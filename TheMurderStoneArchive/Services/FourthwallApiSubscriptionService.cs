using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace TheMurderStoneArchive.Services
{
    /// <summary>
    /// Handles Fourthwall subscription webhook events for API premium access.
    /// When a user purchases an API subscription via Fourthwall, this service:
    /// 1. Receives the webhook event
    /// 2. Links the purchase to a user's API key
    /// 3. Upgrades the API key to premium tier
    /// 4. Sets subscription expiry date
    /// </summary>
    public interface IFourthwallApiSubscriptionService
    {
        /// <summary>
        /// Process a Fourthwall subscription purchase webhook.
        /// Expected to receive customer email and upgrade any free API keys to premium.
        /// </summary>
        Task<bool> ProcessSubscriptionPurchaseAsync(string customerEmail, string externalOrderId, DateTime subscriptionExpiresAtUtc);

        /// <summary>
        /// Process a subscription cancellation (downgrade API key to free).
        /// </summary>
        Task<bool> ProcessSubscriptionCancellationAsync(string customerEmail, string externalOrderId);

        /// <summary>
        /// Find or create an API key for a user, linked to a Fourthwall subscription.
        /// </summary>
        Task<ApiKey?> GetOrCreateSubscriptionApiKeyAsync(string userId, string subscriptionId, DateTime expiresAtUtc);
    }

    public class FourthwallApiSubscriptionService : IFourthwallApiSubscriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IApiAuthenticationService _apiAuthService;
        private readonly ILogger<FourthwallApiSubscriptionService> _logger;

        public FourthwallApiSubscriptionService(
            ApplicationDbContext context,
            IApiAuthenticationService apiAuthService,
            ILogger<FourthwallApiSubscriptionService> logger)
        {
            _context = context;
            _apiAuthService = apiAuthService;
            _logger = logger;
        }

        /// <summary>
        /// Process a Fourthwall subscription purchase.
        /// Finds the user by email and upgrades their API key to premium.
        /// </summary>
        public async Task<bool> ProcessSubscriptionPurchaseAsync(
            string customerEmail,
            string externalOrderId,
            DateTime subscriptionExpiresAtUtc)
        {
            if (string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(externalOrderId))
                return false;

            try
            {
                customerEmail = customerEmail.Trim();
                externalOrderId = externalOrderId.Trim();
                var normalizedEmail = customerEmail.ToUpperInvariant();

                // Find user by normalized email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

                if (user == null)
                {
                    _logger.LogWarning($"Fourthwall subscription purchase: user not found for email {customerEmail}");
                    return false;
                }

                // Find or create the Subscription record
                var existingSubscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.ExternalId == externalOrderId);

                long subscriptionId;
                if (existingSubscription != null)
                {
                    // Renewal: update expiry
                    existingSubscription.ExpiresAtUtc = subscriptionExpiresAtUtc;
                    existingSubscription.CancelledAtUtc = null; // reactivated
                    existingSubscription.UpdatedAtUtc = DateTime.UtcNow;
                    _context.Subscriptions.Update(existingSubscription);
                    await _context.SaveChangesAsync();
                    subscriptionId = existingSubscription.Id;
                }
                else
                {
                    var subscription = new Subscription
                    {
                        ExternalId = externalOrderId,
                        ContributorEmail = customerEmail,
                        MonthlyAmountGbp = 4.00m,
                        StartedAtUtc = DateTime.UtcNow,
                        ExpiresAtUtc = subscriptionExpiresAtUtc,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    _context.Subscriptions.Add(subscription);
                    await _context.SaveChangesAsync();
                    subscriptionId = subscription.Id;
                }

                // Upgrade or create API key
                var apiKey = await GetOrCreateSubscriptionApiKeyAsync(
                    user.Id,
                    externalOrderId,
                    subscriptionExpiresAtUtc);

                if (apiKey != null)
                {
                    _logger.LogInformation("Fourthwall subscription activated for user {UserId}, API key {KeyId} upgraded to Premium, expires {Expires}",
                        user.Id, apiKey.Id, subscriptionExpiresAtUtc);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Fourthwall subscription for email {customerEmail}");
                return false;
            }
        }

        /// <summary>
        /// Process a subscription cancellation.
        /// Downgrade the user's API key back to free tier.
        /// </summary>
        public async Task<bool> ProcessSubscriptionCancellationAsync(
            string customerEmail,
            string externalOrderId)
        {
            if (string.IsNullOrWhiteSpace(customerEmail) || string.IsNullOrWhiteSpace(externalOrderId))
                return false;

            try
            {
                customerEmail = customerEmail.Trim();
                externalOrderId = externalOrderId.Trim();
                var normalizedEmail = customerEmail.ToUpperInvariant();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

                if (user == null)
                {
                    _logger.LogWarning($"Fourthwall subscription cancellation: user not found for email {customerEmail}");
                    return false;
                }

                // Find the premium API key linked to this subscription
                var apiKey = await _context.ApiKeys
                    .Include(k => k.Subscription)
                    .FirstOrDefaultAsync(k =>
                        k.UserId == user.Id &&
                        k.Tier == ApiKeyTier.Premium &&
                        k.Subscription != null &&
                        k.Subscription.ExternalId == externalOrderId);

                if (apiKey != null)
                {
                    // Mark the subscription as cancelled
                    if (apiKey.Subscription != null)
                    {
                        apiKey.Subscription.CancelledAtUtc = DateTime.UtcNow;
                        apiKey.Subscription.UpdatedAtUtc = DateTime.UtcNow;
                        _context.Subscriptions.Update(apiKey.Subscription);
                    }

                    // Downgrade to free
                    apiKey.Tier = ApiKeyTier.Free;
                    apiKey.SubscriptionId = null;
                    apiKey.SubscriptionExpiresAtUtc = null;
                    _context.ApiKeys.Update(apiKey);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"API key {apiKey.Id} downgraded to Free tier due to subscription cancellation");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Fourthwall subscription cancellation for email {customerEmail}");
                return false;
            }
        }

        /// <summary>
        /// Get or create an API key for a user, linked to a subscription.
        /// If user has no API key, creates one. Upgrades to premium if needed.
        /// </summary>
        public async Task<ApiKey?> GetOrCreateSubscriptionApiKeyAsync(
            string userId,
            string subscriptionId,
            DateTime expiresAtUtc)
        {
            try
            {
                // Check if user already has a premium API key for this subscription
                var existingPremiumKey = await _context.ApiKeys
                    .Include(k => k.Subscription)
                    .FirstOrDefaultAsync(k =>
                        k.UserId == userId &&
                        k.Tier == ApiKeyTier.Premium &&
                        k.Subscription != null &&
                        k.Subscription.ExternalId == subscriptionId);

                if (existingPremiumKey != null)
                {
                    existingPremiumKey.SubscriptionExpiresAtUtc = expiresAtUtc;
                    _context.ApiKeys.Update(existingPremiumKey);
                    await _context.SaveChangesAsync();
                    return existingPremiumKey;
                }

                // Always create a new Premium key for a new subscription purchase
                var (_, newKey) = await _apiAuthService.GenerateApiKeyAsync(
                    userId,
                    "API Premium Subscription",
                    ApiKeyTier.Premium);

                var sub = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.ExternalId == subscriptionId);

                if (sub != null)
                {
                    newKey.SubscriptionId = sub.Id;
                    newKey.SubscriptionExpiresAtUtc = expiresAtUtc;
                    _context.ApiKeys.Update(newKey);
                    await _context.SaveChangesAsync();
                }

                return newKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/upgrading API key for subscription {SubscriptionId}", subscriptionId);
                return null;
            }
        }
    }
}
