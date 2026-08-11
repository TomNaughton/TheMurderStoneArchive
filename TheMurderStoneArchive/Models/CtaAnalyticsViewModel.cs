namespace TheMurderStoneArchive.Models
{
    public class CtaAnalyticsViewModel
    {
        public IReadOnlyList<CtaTotalItem> TotalsByCta { get; set; } = Array.Empty<CtaTotalItem>();

        public IReadOnlyList<CtaDailyTrendItem> DailyTrend { get; set; } = Array.Empty<CtaDailyTrendItem>();

        public IReadOnlyList<CtaRecentEventItem> RecentEvents { get; set; } = Array.Empty<CtaRecentEventItem>();
    }

    public class CtaTotalItem
    {
        public string CtaKey { get; set; } = string.Empty;

        public int Clicks { get; set; }
    }

    public class CtaDailyTrendItem
    {
        public DateOnly Date { get; set; }

        public int Clicks { get; set; }
    }

    public class CtaRecentEventItem
    {
        public string CtaKey { get; set; } = string.Empty;

        public DateTime ClickedAtUtc { get; set; }

        public string? Path { get; set; }

        public string? Referrer { get; set; }

        public string? UserId { get; set; }
    }
}
