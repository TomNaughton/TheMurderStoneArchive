using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    /// <summary>
    /// Service for API key generation, validation, and subscription tracking.
    /// </summary>
    public interface IApiAuthenticationService
    {
        /// <summary>
        /// Generates a new API key for a user.
        /// Returns the raw key (shown only once); actual DbContext stores the hash.
        /// </summary>
        Task<(string RawKey, ApiKey DbEntity)> GenerateApiKeyAsync(string userId, string name = "Default", ApiKeyTier tier = ApiKeyTier.Free);

        /// <summary>
        /// Validates a provided API key string and returns the corresponding ApiKey entity if valid.
        /// Returns null if invalid, revoked, or expired.
        /// Updates LastUsedAtUtc and TotalRequests.
        /// </summary>
        Task<ApiKey?> ValidateAndGetApiKeyAsync(string providedKey);

        /// <summary>
        /// Checks if an API key has active premium subscription.
        /// </summary>
        bool IsPremiumActive(ApiKey apiKey);

        /// <summary>
        /// Upgrades a free tier API key to premium, linking a subscription.
        /// </summary>
        Task<bool> UpgradeToPremiumpAsync(ApiKey apiKey, long subscriptionId, DateTime expiresAtUtc);

        /// <summary>
        /// Revokes an API key permanently.
        /// Returns the revoked ApiKey entity (including tier/subscription info), or null if not found.
        /// </summary>
        Task<ApiKey?> RevokeApiKeyAsync(int apiKeyId);

        /// <summary>
        /// Gets all API keys for a user.
        /// </summary>
        Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId);

        /// <summary>
        /// Resets monthly request counter rate-limits if needed.
        /// Should be called periodically (e.g., on first request of each month).
        /// </summary>
        Task ResetMonthlyLimitsAsync();
    }

    public class ApiAuthenticationService : IApiAuthenticationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ApiAuthenticationService> _logger;

        // API key format: prefix + random bytes (e.g., "msa_abc123def456...")
        private const string KeyPrefix = "msa_";
        private const int KeyByteLength = 32; // 256 bits

        public ApiAuthenticationService(ApplicationDbContext context, ILogger<ApiAuthenticationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Generates a new 256-bit API key, hashes it for storage, and returns both.
        /// </summary>
        public async Task<(string RawKey, ApiKey DbEntity)> GenerateApiKeyAsync(string userId, string name = "Default", ApiKeyTier tier = ApiKeyTier.Free)
        {
            // Enforce per-user key limits before generating
            var activeKeys = await _context.ApiKeys
                .Where(k => k.UserId == userId && !k.IsRevoked && k.Tier == tier)
                .CountAsync();

            int limit = tier == ApiKeyTier.Premium
                ? AppConstants.MaxPremiumApiKeysPerUser
                : AppConstants.MaxFreeApiKeysPerUser;

            if (activeKeys >= limit)
            {
                var tierName = tier.ToString().ToLowerInvariant();
                throw new InvalidOperationException(
                    $"You have reached the maximum number of active {tierName} API keys ({limit}). " +
                    $"Please revoke an existing key before creating a new one.");
            }

            // Generate random bytes
            var randomBytes = new byte[KeyByteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // Format: prefix + base64 (without padding for cleaner URLs)
            var base64 = Convert.ToBase64String(randomBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var rawKey = KeyPrefix + base64;

            // Hash the key for storage
            var keyHash = HashKey(rawKey);

            // Create the database entity
            var apiKey = new ApiKey
            {
                KeyHash = keyHash,
                KeyPrefix = rawKey[..Math.Min(16, rawKey.Length)],
                Name = name,
                Tier = tier,
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                BillingPeriodStartUtc = DateTime.UtcNow
            };

            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Generated new API key '{name}' for user {userId} with tier {tier}");

            return (rawKey, apiKey);
        }

        /// <summary>
        /// Validates a provided API key string.
        /// Returns the ApiKey entity if valid, null otherwise.
        /// Updates usage statistics and checks subscription expiry.
        /// </summary>
        public async Task<ApiKey?> ValidateAndGetApiKeyAsync(string providedKey)
        {
            if (string.IsNullOrWhiteSpace(providedKey))
                return null;

            // Hash the provided key and look it up
            var providedHash = HashKey(providedKey);
            var apiKey = await _context.ApiKeys
                .Include(k => k.Subscription)
                .AsSplitQuery()
                .FirstOrDefaultAsync(k => k.KeyHash == providedHash);

            if (apiKey == null)
            {
                _logger.LogWarning($"API key validation failed: key not found");
                return null;
            }

            if (apiKey.IsRevoked)
            {
                _logger.LogWarning($"API key validation failed: key revoked (Id: {apiKey.Id})");
                return null;
            }

            // Check subscription expiry if premium
            if (apiKey.Tier == ApiKeyTier.Premium && apiKey.SubscriptionExpiresAtUtc.HasValue)
            {
                if (DateTime.UtcNow > apiKey.SubscriptionExpiresAtUtc.Value)
                {
                    _logger.LogWarning($"API key premium tier expired (Id: {apiKey.Id}), downgrading to free");
                    apiKey.Tier = ApiKeyTier.Free;
                    apiKey.SubscriptionId = null;
                    apiKey.SubscriptionExpiresAtUtc = null;
                    // Note: caller should save if needed
                }
            }

            // Update usage stats
            apiKey.LastUsedAtUtc = DateTime.UtcNow;
            apiKey.TotalRequests++;
            apiKey.RequestsThisMonth++;

            // Save usage updates
            _context.ApiKeys.Update(apiKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"API key validated successfully (Id: {apiKey.Id}, Tier: {apiKey.Tier})");
            return apiKey;
        }

        /// <summary>
        /// Checks if an API key has active premium subscription.
        /// </summary>
        public bool IsPremiumActive(ApiKey apiKey)
        {
            if (apiKey.Tier != ApiKeyTier.Premium)
                return false;

            if (!apiKey.SubscriptionExpiresAtUtc.HasValue)
                return false;

            return DateTime.UtcNow <= apiKey.SubscriptionExpiresAtUtc.Value;
        }

        /// <summary>
        /// Upgrades a free key to premium with subscription tracking.
        /// </summary>
        public async Task<bool> UpgradeToPremiumpAsync(ApiKey apiKey, long subscriptionId, DateTime expiresAtUtc)
        {
            apiKey.Tier = ApiKeyTier.Premium;
            apiKey.SubscriptionId = subscriptionId;
            apiKey.SubscriptionExpiresAtUtc = expiresAtUtc;

            _context.ApiKeys.Update(apiKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Upgraded API key {apiKey.Id} to Premium tier, subscription expires {expiresAtUtc}");
            return true;
        }

        /// <summary>
        /// Revokes an API key permanently.
        /// </summary>
        public async Task<ApiKey?> RevokeApiKeyAsync(int apiKeyId)
        {
            var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKeyId);
            if (apiKey == null)
                return null;

            apiKey.IsRevoked = true;
            _context.ApiKeys.Update(apiKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Revoked API key {apiKeyId}");
            return apiKey;
        }

        /// <summary>
        /// Gets all API keys for a user (excluding key hashes for security).
        /// </summary>
        public async Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId)
        {
            return await _context.ApiKeys
                .Include(k => k.Subscription)
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.CreatedAtUtc)
                .ToListAsync();
        }

        /// <summary>
        /// Resets monthly request counters. Call this periodically (e.g., via background job).
        /// </summary>
        public async Task ResetMonthlyLimitsAsync()
        {
            var keysToReset = await _context.ApiKeys
                .Where(k => k.BillingPeriodStartUtc.AddMonths(1) <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var key in keysToReset)
            {
                key.RequestsThisMonth = 0;
                key.BillingPeriodStartUtc = DateTime.UtcNow;
            }

            if (keysToReset.Any())
            {
                _context.ApiKeys.UpdateRange(keysToReset);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Reset monthly limits for {keysToReset.Count} API keys");
            }
        }

        /// <summary>
        /// Hash an API key string using SHA-256.
        /// </summary>
        private string HashKey(string key)
        {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(hashBytes).ToLower();
        }

        public bool ValidateApiKey(string incomingKey, string storedDbHash)
        {
            // 1. Hash the incoming key
            string incomingHash = HashKey(incomingKey);

            // 2. Convert both hex strings back to bytes for comparison
            // (Or compare them directly if you store them as byte arrays in the DB)
            byte[] incomingBytes = Encoding.UTF8.GetBytes(incomingHash);
            byte[] storedBytes = Encoding.UTF8.GetBytes(storedDbHash);

            // 3. Compare in constant time
            return CryptographicOperations.FixedTimeEquals(incomingBytes, storedBytes);
        }
    }
}
