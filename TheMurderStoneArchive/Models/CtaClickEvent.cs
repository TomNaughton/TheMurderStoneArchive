namespace TheMurderStoneArchive.Models
{
    public class CtaClickEvent
    {
        public long Id { get; set; }

        public string CtaKey { get; set; } = string.Empty;

        public DateTime ClickedAtUtc { get; set; } = DateTime.UtcNow;

        public string? Path { get; set; }

        public string? Referrer { get; set; }

        public string? UserId { get; set; }
    }
}
