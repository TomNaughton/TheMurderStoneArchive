namespace TheMurderStoneArchive.Models
{
    public class MonetaryContribution
    {
        public long Id { get; set; }

        public long? DonationCampaignId { get; set; }

        public DonationCampaign? DonationCampaign { get; set; }

        public decimal AmountGbp { get; set; }

        public string Currency { get; set; } = "GBP";

        public string Source { get; set; } = "Stripe";

        public string? ProviderSessionId { get; set; }

        public string? ProviderPaymentIntentId { get; set; }

        public string? ProviderChargeId { get; set; }

        public string? ContributorName { get; set; }

        public string? ContributorEmail { get; set; }

        public string? Note { get; set; }

        public bool IsCountedInTotal { get; set; } = true;

        public bool IsManualEntry { get; set; }

        public string Status { get; set; } = "Submitted";

        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ReceivedAtUtc { get; set; }
    }
}
