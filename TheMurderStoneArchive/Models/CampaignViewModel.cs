namespace TheMurderStoneArchive.Models
{
    public class CampaignViewModel
    {
        public long CampaignId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal TargetAmountGbp { get; set; }

        public decimal RaisedAmountGbp { get; set; }

        public decimal ProgressPercentage { get; set; }

        public DateTime? EndsAtUtc { get; set; }

        public string PaymentProvider { get; set; } = "Stripe";

        public string? PatreonCampaignUrl { get; set; }

        public string? FourthwallOneTimePaymentUrl { get; set; }

        public bool UsePatreon { get; set; }

        public bool UseFourthwall { get; set; }

        public IReadOnlyList<CampaignContributionViewModel> RecentContributions { get; set; } = [];
    }

    public class CampaignContributionViewModel
    {
        public decimal AmountGbp { get; set; }

        public string Source { get; set; } = string.Empty;

        public DateTime SubmittedAtUtc { get; set; }
    }
}
