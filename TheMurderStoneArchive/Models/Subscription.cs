using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// Represents a recurring Fourthwall API membership subscription.
    /// The total amount contributed is calculated as MonthlyAmountGbp × whole months
    /// elapsed since StartedAtUtc, capped at the cancellation date if the subscription
    /// has ended.
    /// </summary>
    public class Subscription
    {
        public long Id { get; set; }

        /// <summary>The external subscription/order ID from Fourthwall.</summary>
        [Required]
        [StringLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        /// <summary>The email address the subscriber used on Fourthwall.</summary>
        [StringLength(255)]
        public string? ContributorEmail { get; set; }

        /// <summary>Monthly subscription cost in GBP (defaults to £4).</summary>
        public decimal MonthlyAmountGbp { get; set; } = 4.00m;

        /// <summary>When the subscription became active.</summary>
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the current paid period expires (updated each billing cycle renewal).
        /// Null means the subscription has not yet been given an expiry.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>
        /// When the subscription was cancelled/expired.
        /// Null means it is still active (or was never cancelled).
        /// </summary>
        public DateTime? CancelledAtUtc { get; set; }

        /// <summary>Active = subscription period has not yet lapsed and was not cancelled.</summary>
        public bool IsActive =>
            ExpiresAtUtc.HasValue &&
            DateTime.UtcNow <= ExpiresAtUtc.Value &&
            CancelledAtUtc == null;

        /// <summary>
        /// Whole months elapsed from StartedAtUtc to now (or CancelledAtUtc if cancelled),
        /// minimum 1 to count the first month.
        /// </summary>
        public int MonthsElapsed
        {
            get
            {
                var end = CancelledAtUtc ?? DateTime.UtcNow;
                var months = ((end.Year - StartedAtUtc.Year) * 12) + end.Month - StartedAtUtc.Month;
                return Math.Max(1, months);
            }
        }

        /// <summary>
        /// Running total contribution: MonthlyAmountGbp × MonthsElapsed.
        /// </summary>
        public decimal TotalAmountGbp => MonthlyAmountGbp * MonthsElapsed;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
