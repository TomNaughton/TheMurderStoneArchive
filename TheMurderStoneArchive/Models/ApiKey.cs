using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// Represents an API key for accessing the Murder Stone Archive API.
    /// Free tier: basic data only, rate limited.
    /// Premium tier: full data + analysis, higher limits.
    /// </summary>
    public class ApiKey
    {
        public int Id { get; set; }

        /// <summary>
        /// The actual API key string (hashed for storage)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string KeyHash { get; set; } = string.Empty;

        /// <summary>
        /// The first 16 characters of the raw key (safe to display; not enough to reconstruct the secret)
        /// </summary>
        [StringLength(20)]
        public string? KeyPrefix { get; set; }

        /// <summary>
        /// Friendly name for this key (e.g., "Research Project 2025")
        /// </summary>
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Subscription tier: Free or Premium
        /// </summary>
        public ApiKeyTier Tier { get; set; } = ApiKeyTier.Free;

        /// <summary>
        /// Link to the user who owns this API key
        /// </summary>
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        /// <summary>
        /// Link to the active subscription (if Premium tier).
        /// null for Free tier.
        /// </summary>
        public long? SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        /// <summary>
        /// Subscription end date (for checking if premium access is still active)
        /// </summary>
        public DateTime? SubscriptionExpiresAtUtc { get; set; }

        /// <summary>
        /// When the API key was created
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the API key was last used (for cleanup of stale keys)
        /// </summary>
        public DateTime? LastUsedAtUtc { get; set; }

        /// <summary>
        /// If true, this key is disabled and cannot be used
        /// </summary>
        public bool IsRevoked { get; set; } = false;

        /// <summary>
        /// Total API requests made with this key (life-to-date)
        /// </summary>
        public long TotalRequests { get; set; } = 0;

        /// <summary>
        /// Requests made in the current billing period
        /// Reset monthly
        /// </summary>
        public long RequestsThisMonth { get; set; } = 0;

        /// <summary>
        /// Start of the current billing period
        /// </summary>
        public DateTime BillingPeriodStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// IP addresses that have used this key (comma-separated for simple logging)
        /// </summary>
        public string? IpAddresses { get; set; }
    }

    public enum ApiKeyTier
    {
        Free,
        Premium
    }
}
