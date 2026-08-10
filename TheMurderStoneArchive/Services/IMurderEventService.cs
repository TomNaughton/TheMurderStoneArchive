using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    /// <summary>
    /// Service for managing murder event operations including search, filtering, and data retrieval.
    /// </summary>
    public interface IMurderEventService
    {
        /// <summary>
        /// Gets paginated murder events based on search and sort criteria.
        /// </summary>
        Task<(List<MurderEvent> Events, int TotalCount)> GetEventsAsync(
            string? searchTerm = null,
            string sortOrder = "title",
            int page = 1,
            int pageSize = 10,
            string? currentUserId = null);

        /// <summary>
        /// Gets a specific murder event by ID with all related data.
        /// </summary>
        Task<MurderEvent?> GetEventByIdAsync(int id, string? currentUserId = null);

        /// <summary>
        /// Verifies a reCAPTCHA token.
        /// </summary>
        Task<bool> VerifyReCaptchaAsync(string token, string? expectedAction = null, double minScore = 0.5);

        /// <summary>
        /// Extracts YouTube video ID from various YouTube URL formats.
        /// </summary>
        string? ExtractYouTubeId(string url);
    }
}
