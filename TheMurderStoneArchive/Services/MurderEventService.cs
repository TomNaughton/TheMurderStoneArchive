using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    /// <summary>
    /// Implementation of IMurderEventService for managing murder event operations.
    /// </summary>
    public class MurderEventService : IMurderEventService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MurderEventService> _logger;

        public MurderEventService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<MurderEventService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Gets paginated murder events based on search and sort criteria.
        /// </summary>
        public async Task<(List<MurderEvent> Events, int TotalCount)> GetEventsAsync(
            string? searchTerm = null,
            string sortOrder = "title",
            int page = 1,
            int pageSize = 10,
            string? currentUserId = null)
        {
            var query = _context.MurderEvents
                .Include(m => m.Location)
                .Where(m => m.IsApproved || (!string.IsNullOrEmpty(m.CreatedById) && m.CreatedById == currentUserId))
                .AsQueryable();

            // Apply search filter.
            // Npgsql supports case-insensitive ILIKE (index-friendly with pg_trgm), while
            // providers without ILIKE support (e.g. EF InMemory used in tests) fall back
            // to a client-lowered Contains comparison.
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                if (_context.Database.IsNpgsql())
                {
                    var pattern = $"%{term}%";
                    query = query.Where(m => EF.Functions.ILike(m.Title, pattern) ||
                                             EF.Functions.ILike(m.Description, pattern));
                }
                else
                {
                    var lowerTerm = term.ToLower();
                    query = query.Where(m => m.Title.ToLower().Contains(lowerTerm) ||
                                             m.Description.ToLower().Contains(lowerTerm));
                }
            }

            // Apply sorting
            query = sortOrder switch
            {
                AppConstants.SortOrderYearAsc => query.OrderBy(m => m.Year),
                AppConstants.SortOrderYearDesc => query.OrderByDescending(m => m.Year),
                AppConstants.SortOrderLocation => query.OrderBy(m => m.Location.Name),
                AppConstants.SortOrderTitleDesc => query.OrderByDescending(m => m.Title),
                _ => query.OrderBy(m => m.Title) // Default: sort by title ascending
            };

            // Get total count before pagination
            var totalEvents = await query.CountAsync();

            // Apply pagination
            var events = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (events, totalEvents);
        }

        /// <summary>
        /// Gets a specific murder event by ID with all related data.
        /// </summary>
        public async Task<MurderEvent?> GetEventByIdAsync(int id, string? currentUserId = null)
        {
            var murderEvent = await _context.MurderEvents
                .Include(m => m.Location)
                .Include(m => m.Monuments)
                .Include(m => m.Perpetrators)
                .Include(m => m.Photos)
                .Include(m => m.Videos)
                .Include(m => m.CreatedBy)
                .Include(m => m.Comments).ThenInclude(c => c.User)
                .Include(m => m.ChangeLogEntries).ThenInclude(c => c.Contributor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (murderEvent == null)
                return null;

            // Check if user has permission to view
            if (!murderEvent.IsApproved && (currentUserId == null || murderEvent.CreatedById != currentUserId))
                return null;

            return murderEvent;
        }

        /// <summary>
        /// Verifies a reCAPTCHA token.
        /// </summary>
        public async Task<bool> VerifyReCaptchaAsync(string token, string? expectedAction = null, double minScore = AppConstants.ReCaptchaDefaultMinScore)
        {
            try
            {
                var secret = _configuration[AppConstants.ReCaptchaSecretKeyKey];
                if (string.IsNullOrEmpty(secret))
                {
                    _logger.LogWarning("ReCaptcha secret key not configured");
                    return false;
                }

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(AppConstants.ReCaptchaTimeoutSeconds);

                var values = new Dictionary<string, string>
                {
                    {"secret", secret},
                    {"response", token}
                };
                var content = new FormUrlEncodedContent(values);
                var resp = await client.PostAsync(AppConstants.ReCaptchaVerifyUrl, content);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ReCaptcha API returned status {StatusCode}", resp.StatusCode);
                    return false;
                }

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                {
                    _logger.LogWarning("ReCaptcha verification failed: success=false");
                    return false;
                }

                // Only check score for reCAPTCHA v3 (v2 doesn't return a score)
                if (doc.RootElement.TryGetProperty("score", out var scoreElem) && scoreElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    double score = scoreElem.GetDouble();
                    if (score < minScore)
                    {
                        _logger.LogWarning("ReCaptcha score {Score} is below minimum {MinScore}", score, minScore);
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(expectedAction))
                {
                    if (doc.RootElement.TryGetProperty("action", out var actionElem))
                    {
                        var action = actionElem.GetString();
                        if (!string.Equals(action, expectedAction, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("ReCaptcha action mismatch: expected {ExpectedAction}, got {Action}", expectedAction, action);
                            return false;
                        }
                    }
                }

                _logger.LogInformation("ReCaptcha verification successful");
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "ReCaptcha HTTP request failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during ReCaptcha verification");
                return false;
            }
        }

        /// <summary>
        /// Extracts YouTube video ID from various YouTube URL formats.
        /// </summary>
        public string? ExtractYouTubeId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var uri = new UriBuilder(url).Uri;
                var host = uri.Host.ToLowerInvariant();
                // youtu.be short link
                if (host.EndsWith(AppConstants.YouTubeShortHost))
                {
                    var seg = uri.AbsolutePath.Trim('/');
                    return string.IsNullOrEmpty(seg) ? null : seg;
                }

                // youtube.com forms
                if (host.Contains(AppConstants.YouTubeHost))
                {
                    // /embed/ID
                    if (uri.AbsolutePath.StartsWith(AppConstants.YouTubeEmbedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var id = uri.AbsolutePath.Substring(AppConstants.YouTubeEmbedPath.Length).Trim('/');
                        return string.IsNullOrEmpty(id) ? null : id;
                    }

                    // parse query without System.Web: use QueryHelpers
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                    if (query.TryGetValue(AppConstants.YouTubeVideoParamKey, out var v) && !string.IsNullOrEmpty(v))
                        return v.ToString();
                }

                // fallback: try regex to find id-like segment
                var m = System.Text.RegularExpressions.Regex.Match(url, AppConstants.YouTubeIdRegexPattern);
                if (m.Success && m.Groups.Count > 1) return m.Groups[1].Value;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing YouTube URL: {YouTubeUrl}", url);
                return null;
            }
        }
    }
}
