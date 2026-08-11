namespace TheMurderStoneArchive.Models
{
    public class DonationCampaign
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal TargetAmountGbp { get; set; }

        public decimal RaisedAmountGbp { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EndsAtUtc { get; set; }

        public List<MonetaryContribution> Contributions { get; set; } = [];
    }
}
