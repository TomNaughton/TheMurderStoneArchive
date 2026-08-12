using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Models.Dtos;
using TheMurderStoneArchive.Services;

namespace TheMurderStoneArchive.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public class EventsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IApiAuthenticationService _authService;
        private readonly ILogger<EventsController> _logger;

        // Rate limiting
        private const int FreeRateLimit = 100; // requests per month
        private const int PremiumRateLimit = 10000; // requests per month

        public EventsController(
            ApplicationDbContext context,
            IApiAuthenticationService authService,
            ILogger<EventsController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// List all murder stone events with pagination.
        /// Free tier returns basic data; premium tier returns full details.
        /// </summary>
        [HttpGet(Name = "GetEvents")]
        public async Task<ActionResult<ApiResponse<object>>> GetEvents(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            var validKey = await ValidateApiKeyAsync();
            if (validKey.IsError)
                return validKey.ErrorResult;

            var key = validKey.Value!;

            var rateLimitCheck = CheckRateLimit(key);
            if (!rateLimitCheck.IsAllowed)
                return StatusCode(429, new ApiErrorResponse 
                { 
                    Message = $"Rate limit exceeded. {rateLimitCheck.Message}",
                    ErrorCode = "RATE_LIMIT_EXCEEDED"
                });

            pageSize = Math.Min(pageSize, 500);
            pageSize = Math.Max(pageSize, 1);
            pageNumber = Math.Max(pageNumber, 1);

            var query = _context.MurderEvents
                .Where(e => e.IsApproved)
                .Include(e => e.Location)
                .Include(e => e.Perpetrators)
                .Include(e => e.Monuments);

            var total = await query.CountAsync();
            var events = await query
                .OrderBy(e => e.Year)
                .ThenBy(e => e.Title)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var isFreeOnly = key.Tier == ApiKeyTier.Free;
            var dtoList = events.Select(e => (object)(isFreeOnly
                ? MapToBasicDto(e)
                : MapToPremiumDto(e)))
            .ToList();

            _logger.LogInformation($"API: Listed {dtoList.Count} events (page {pageNumber}) for key {key.Id}");

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = dtoList,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// Search for murder stone events by title or location.
        /// </summary>
        [HttpGet("search", Name = "SearchEvents")]
        public async Task<ActionResult<ApiResponse<object>>> SearchEvents(
            [FromQuery] string? query = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            var validKey = await ValidateApiKeyAsync();
            if (validKey.IsError)
                return validKey.ErrorResult;

            var key = validKey.Value!;

            var rateLimitCheck = CheckRateLimit(key);
            if (!rateLimitCheck.IsAllowed)
                return StatusCode(429, new ApiErrorResponse 
                { 
                    Message = $"Rate limit exceeded. {rateLimitCheck.Message}",
                    ErrorCode = "RATE_LIMIT_EXCEEDED"
                });

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return BadRequest(new ApiErrorResponse 
                { 
                    Message = "Search query must be at least 2 characters",
                    ErrorCode = "INVALID_QUERY"
                });

            query = query.Trim();

            var isFreeOnly = key.Tier == ApiKeyTier.Free;
            var searchQuery = _context.MurderEvents
                .Where(e => e.IsApproved && (
                    EF.Functions.Like(e.Title, $"%{query}%") ||
                    EF.Functions.Like(e.Description, $"%{query}%") ||
                    EF.Functions.Like(e.Location.Name, $"%{query}%")))
                .Include(e => e.Location)
                .Include(e => e.Perpetrators)
                .Include(e => e.Monuments);

            var total = await searchQuery.CountAsync();
            var events = await searchQuery
                .OrderBy(e => e.Year)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtoList = events.Select(e => (object)(isFreeOnly
                ? MapToBasicDto(e)
                : MapToPremiumDto(e)))
            .ToList();

            _logger.LogInformation($"API: Searched for '{query}' - found {total} results for key {key.Id}");

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = dtoList,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// Get analysis (clusters, trends, region insights) - premium only.
        /// </summary>
        [HttpGet("analysis", Name = "GetAnalysis")]
        public async Task<ActionResult> GetAnalysis()
        {
            var validKey = await ValidateApiKeyAsync();
            if (validKey.IsError)
                return validKey.ErrorResult;

            var key = validKey.Value!;

            if (key.Tier != ApiKeyTier.Premium)
                return Forbid();

            var rateLimitCheck = CheckRateLimit(key);
            if (!rateLimitCheck.IsAllowed)
                return StatusCode(429, new ApiErrorResponse 
                { 
                    Message = $"Rate limit exceeded. {rateLimitCheck.Message}",
                    ErrorCode = "RATE_LIMIT_EXCEEDED"
                });

            var events = await _context.MurderEvents
                .Where(e => e.IsApproved)
                .Include(e => e.Location)
                .Include(e => e.Perpetrators)
                .ToListAsync();

            // ── Location clusters (greedy ~50 km radius grouping) ─────────────
            const double ClusterRadiusKm = 50.0;
            var ungrouped = events
                .Where(e => e.Location != null)
                .ToList();
            var clusters = new List<LocationClusterDto>();
            var assigned = new HashSet<int>();
            int clusterId = 1;

            foreach (var evt in ungrouped)
            {
                if (assigned.Contains(evt.Id)) continue;
                var clusterEvents = ungrouped
                    .Where(e => !assigned.Contains(e.Id) &&
                                HaversineKm(evt.Location.Latitude, evt.Location.Longitude,
                                            e.Location.Latitude, e.Location.Longitude) <= ClusterRadiusKm)
                    .ToList();
                foreach (var ce in clusterEvents) assigned.Add(ce.Id);

                var centerLat = clusterEvents.Average(e => e.Location.Latitude);
                var centerLng = clusterEvents.Average(e => e.Location.Longitude);
                var region = InferRegion(evt.Location.Name) ?? "Unknown";

                clusters.Add(new LocationClusterDto
                {
                    ClusterId = clusterId++,
                    ClusterName = region != "Unknown" ? $"{region} cluster" : $"Cluster {clusterId - 1}",
                    CenterLatitude = Math.Round(centerLat, 4),
                    CenterLongitude = Math.Round(centerLng, 4),
                    EventCount = clusterEvents.Count,
                    Locations = clusterEvents.Select(e => new Models.Dtos.LocationDto
                    {
                        Id = e.Location.Id,
                        Name = e.Location.Name,
                        Latitude = e.Location.Latitude,
                        Longitude = e.Location.Longitude
                    }).ToList()
                });
            }

            // ── Temporal trends (50-year buckets) ────────────────────────────
            var trends = new List<TrendDto>();
            if (events.Any())
            {
                int earliest = events.Min(e => e.Year);
                int latest   = events.Max(e => e.Year);
                int bucketSize = 50;
                int start = (earliest / bucketSize) * bucketSize;
                for (int y = start; y <= latest; y += bucketSize)
                {
                    int end   = y + bucketSize - 1;
                    int count = events.Count(e => e.Year >= y && e.Year <= end);
                    if (count == 0) continue;
                    trends.Add(new TrendDto
                    {
                        Description = $"Events in {y}–{end}",
                        StartYear   = y,
                        EndYear     = end,
                        EventCount  = count,
                        Percentage  = Math.Round((double)count / events.Count * 100, 1)
                    });
                }
            }

            // ── Region insights ───────────────────────────────────────────────
            var regionGroups = events
                .Where(e => e.Location != null)
                .GroupBy(e => InferRegion(e.Location.Name) ?? "Unknown")
                .Where(g => g.Key != "Unknown")
                .OrderByDescending(g => g.Count());

            var regionInsights = regionGroups.Select(g =>
            {
                RegionData.Centroids.TryGetValue(g.Key, out var centroid);
                int unprotected = g.Count(e => !e.IsProtected);
                int riskScore   = events.Any()
                    ? (int)Math.Round((double)unprotected / Math.Max(g.Count(), 1) * 100)
                    : 0;

                // Most frequent description theme
                var themeCounts = _trendKeywords
                    .Select(kv => new
                    {
                        Theme = kv.Key,
                        Hits  = g.Sum(e => kv.Value.Count(kw =>
                            e.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    })
                    .OrderByDescending(x => x.Hits)
                    .FirstOrDefault();

                return new RegionInsightDto
                {
                    Region              = g.Key,
                    EventCount          = g.Count(),
                    Latitude            = centroid.Latitude,
                    Longitude           = centroid.Longitude,
                    TopTrend            = themeCounts?.Hits > 0 ? themeCounts.Theme : "No dominant theme",
                    ProtectionRiskScore = riskScore
                };
            }).ToList();

            var analysis = new EventAnalysisDto
            {
                TotalEvents      = events.Count,
                ApprovedEvents   = events.Count,
                ProtectedSites   = events.Count(e => e.IsProtected),
                LostStones       = events.Count(e => e.IsLost),
                EarliestYear     = events.Any() ? events.Min(e => e.Year) : 0,
                LatestYear       = events.Any() ? events.Max(e => e.Year) : 0,
                AverageYear      = events.Any() ? Math.Round(events.Average(e => e.Year), 1) : 0,
                LocationClusters = clusters,
                Trends           = trends,
                RegionInsights   = regionInsights
            };

            _logger.LogInformation($"API: Returned analysis data for key {key.Id}");
            return Ok(analysis);
        }

        /// <summary>
        /// Get a single event by ID.
        /// </summary>
        [HttpGet("{id}", Name = "GetEventById")]
        public async Task<ActionResult> GetEventById(
            [FromRoute] int id)
        {
            var validKey = await ValidateApiKeyAsync();
            if (validKey.IsError)
                return validKey.ErrorResult;

            var key = validKey.Value!;

            var rateLimitCheck = CheckRateLimit(key);
            if (!rateLimitCheck.IsAllowed)
                return StatusCode(429, new ApiErrorResponse 
                { 
                    Message = $"Rate limit exceeded. {rateLimitCheck.Message}",
                    ErrorCode = "RATE_LIMIT_EXCEEDED"
                });

            var evt = await _context.MurderEvents
                .Include(e => e.Location)
                .Include(e => e.Perpetrators)
                .Include(e => e.Monuments)
                .FirstOrDefaultAsync(e => e.Id == id && e.IsApproved);

            if (evt == null)
                return NotFound(new ApiErrorResponse 
                { 
                    Message = "Event not found",
                    ErrorCode = "EVENT_NOT_FOUND"
                });

            var isFreeOnly = key.Tier == ApiKeyTier.Free;
            var dto = isFreeOnly ? (object)MapToBasicDto(evt) : MapToPremiumDto(evt);

            _logger.LogInformation($"API: Retrieved event {id} for key {key.Id}");
            return Ok(dto);
        }

        // ── Analysis helpers ──────────────────────────────────────────────────

        // ── Analysis helpers ──────────────────────────────────────────────────────
        // Region keyword and centroid data is shared via RegionData (Helpers/RegionData.cs).
        // _trendKeywords remain local as they are analysis-specific, not geographic reference data.

        private static readonly Dictionary<string, string[]> _trendKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Interpersonal violence"]     = ["murder", "killed", "assault", "attack", "stab", "shot", "struck"],
            ["Legal and punishment context"] = ["trial", "court", "executed", "gallows", "verdict", "convicted"],
            ["Folklore and oral history"]  = ["legend", "folklore", "ghost", "oral tradition", "myth"],
            ["Religious or ritual framing"] = ["church", "chapel", "holy", "ritual", "saint", "pilgrim"],
            ["Landscape and boundary cues"] = ["boundary", "marker", "crossroads", "moor", "common", "stone"]
        };

        /// <summary>
        /// Returns the canonical region name for a location string using whole-word matching,
        /// with a coordinate-based proximity fallback (≤ 80 km to nearest centroid).
        /// </summary>
        private static string? InferRegion(string locationName, double? latitude = null, double? longitude = null)
        {
            if (!string.IsNullOrWhiteSpace(locationName))
            {
                foreach (var kv in RegionData.Keywords)
                    if (RegionData.ContainsWholeWord(locationName, kv.Key))
                        return kv.Value;
            }

            if (latitude.HasValue && longitude.HasValue)
            {
                const double FallbackRadiusKm = 80.0;
                var nearest = RegionData.Centroids
                    .Select(c => new { c.Key, Dist = HaversineKm(latitude.Value, longitude.Value, c.Value.Latitude, c.Value.Longitude) })
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault();
                if (nearest != null && nearest.Dist <= FallbackRadiusKm)
                    return nearest.Key;
            }

            return null;
        }

        /// <summary>Haversine great-circle distance in km.</summary>
        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private BasicMurderEventDto MapToBasicDto(MurderEvent evt)
        {
            return new BasicMurderEventDto
            {
                Id = evt.Id,
                Title = evt.Title,
                Year = evt.Year,
                Location = evt.Location == null ? null : new Models.Dtos.LocationDto
                {
                    Id = evt.Location.Id,
                    Name = evt.Location.Name,
                    Latitude = evt.Location.Latitude,
                    Longitude = evt.Location.Longitude
                },
                VictimCount = evt.Perpetrators.Count
            };
        }

        private PremiumMurderEventDto MapToPremiumDto(MurderEvent evt)
        {
            return new PremiumMurderEventDto
            {
                Id = evt.Id,
                Title = evt.Title,
                Year = evt.Year,
                Description = evt.Description,
                Category = evt.Category.ToString(),
                Location = evt.Location == null ? null : new Models.Dtos.LocationDto
                {
                    Id = evt.Location.Id,
                    Name = evt.Location.Name,
                    Latitude = evt.Location.Latitude,
                    Longitude = evt.Location.Longitude
                },
                VictimCount = evt.Perpetrators.Count,
                IsApproved = evt.IsApproved,
                IsProtected = evt.IsProtected,
                IsLost = evt.IsLost,
                Perpetrators = evt.Perpetrators.Select(p => new PerpinatorDto
                {
                    Id = p.Id,
                    Name = p.FullName
                }).ToList(),
                MonumentNames = evt.Monuments.Select(m => m.MonumentType).ToList(),
                SubmittedAtUtc = evt.CreatedUtc
            };
        }

        private async Task<ValidationResult> ValidateApiKeyAsync(string? queryKey = null)
        {
            // Prefer X-Api-Key header; fall back to query string for backwards compatibility
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault()
                         ?? queryKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                return ValidationResult.Error(StatusCode(401, new ApiErrorResponse 
                { 
                    Message = "API key is required. Pass it in the X-Api-Key header.",
                    ErrorCode = "MISSING_API_KEY"
                }));

            var validatedKey = await _authService.ValidateAndGetApiKeyAsync(apiKey);
            if (validatedKey == null)
                return ValidationResult.Error(StatusCode(401, new ApiErrorResponse 
                { 
                    Message = "Invalid or revoked API key",
                    ErrorCode = "INVALID_API_KEY"
                }));

            return ValidationResult.Success(validatedKey);
        }

        private (bool IsAllowed, string Message) CheckRateLimit(ApiKey key)
        {
            var limit = key.Tier == ApiKeyTier.Free ? FreeRateLimit : PremiumRateLimit;
            if (key.RequestsThisMonth >= limit)
                return (false, $"Free tier: {FreeRateLimit}/month, Premium: {PremiumRateLimit}/month. Resets on {key.BillingPeriodStartUtc.AddMonths(1):yyyy-MM-dd}");

            return (true, "");
        }

        private class ValidationResult
        {
            public bool IsError { get; set; }
            public ApiKey? Value { get; set; }
            public ObjectResult? ErrorResult { get; set; }

            public static ValidationResult Success(ApiKey value) => 
                new() { IsError = false, Value = value };

            public static ValidationResult Error(ObjectResult error) => 
                new() { IsError = true, ErrorResult = error };
        }
    }
}
