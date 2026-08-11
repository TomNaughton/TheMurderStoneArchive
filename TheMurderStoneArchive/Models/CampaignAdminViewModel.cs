namespace TheMurderStoneArchive.Models
{
    public class CampaignAdminViewModel
    {
        public long CampaignId { get; set; }

        public string CampaignName { get; set; } = string.Empty;

        public decimal TargetAmountGbp { get; set; }

        public decimal RaisedAmountGbp { get; set; }

        public IReadOnlyList<MonetaryContribution> Contributions { get; set; } = [];

        public ManualContributionInput ManualContribution { get; set; } = new();
    }

    public class ManualContributionInput
    {
        public decimal AmountGbp { get; set; }

        public string Source { get; set; } = "Manual";

        public string? ContributorName { get; set; }

        public string? ContributorEmail { get; set; }

        public string? Note { get; set; }
    }
}
