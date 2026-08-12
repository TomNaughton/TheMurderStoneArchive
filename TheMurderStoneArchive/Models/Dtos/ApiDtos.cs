namespace TheMurderStoneArchive.Models.Dtos
{
    /// <summary>
    /// Free tier: basic event data only
    /// </summary>
    public class BasicMurderEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public LocationDto? Location { get; set; }
        public int VictimCount { get; set; }
    }

    /// <summary>
    /// Premium tier: full event details including description, perpetrators, and analysis
    /// </summary>
    public class PremiumMurderEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public LocationDto? Location { get; set; }
        public int VictimCount { get; set; }
        public bool IsApproved { get; set; }
        public bool IsProtected { get; set; }
        public bool IsLost { get; set; }
        public List<PerpinatorDto> Perpetrators { get; set; } = new();
        public List<string> MonumentNames { get; set; } = new();
        public DateTime? SubmittedAtUtc { get; set; }
    }

    /// <summary>
    /// Location DTO (used in all tiers)
    /// </summary>
    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    /// <summary>
    /// Perpetrator info (premium only)
    /// </summary>
    public class PerpinatorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Premium tier: advanced analysis of events (clusters, trends, etc.)
    /// </summary>
    public class EventAnalysisDto
    {
        public int TotalEvents { get; set; }
        public int ApprovedEvents { get; set; }
        public int ProtectedSites { get; set; }
        public int LostStones { get; set; }
        public double AverageYear { get; set; }
        public int EarliestYear { get; set; }
        public int LatestYear { get; set; }
        public List<LocationClusterDto> LocationClusters { get; set; } = new();
        public List<TrendDto> Trends { get; set; } = new();
        public List<RegionInsightDto> RegionInsights { get; set; } = new();
    }

    /// <summary>
    /// Location cluster (proximity-based grouping)
    /// </summary>
    public class LocationClusterDto
    {
        public int ClusterId { get; set; }
        public string ClusterName { get; set; } = string.Empty;
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public int EventCount { get; set; }
        public List<LocationDto> Locations { get; set; } = new();
    }

    /// <summary>
    /// Temporal trend (e.g., "peak activity in 1700-1750")
    /// </summary>
    public class TrendDto
    {
        public string Description { get; set; } = string.Empty;
        public int StartYear { get; set; }
        public int EndYear { get; set; }
        public int EventCount { get; set; }
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Region-level insights (works for counties, provinces, states, or any geographic grouping)
    /// </summary>
    public class RegionInsightDto
    {
        public string Region { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string TopTrend { get; set; } = string.Empty;
        public int ProtectionRiskScore { get; set; } // 0-100
    }

    /// <summary>
    /// API response wrapper with pagination
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? Message { get; set; }
    }

    /// <summary>
    /// Error response
    /// </summary>
    public class ApiErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public Dictionary<string, string>? Details { get; set; }
    }
}
