using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Helpers
{
    /// <summary>
    /// Helper class for YouTube video processing logic.
    /// </summary>
    public static class YouTubeVideoHelper
    {
        private const int MaxYouTubeVideos = 3;

        /// <summary>
        /// Processes a list of YouTube links and updates the database.
        /// Removes existing videos and adds up to 3 new ones.
        /// Only adds videos with valid YouTube IDs.
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="murderEventId">ID of the MurderEvent to associate videos with</param>
        /// <param name="youtubeLinks">List of YouTube URLs to process</param>
        /// <param name="extractYouTubeIdFunc">Function to extract YouTube ID from URL</param>
        public static async Task ProcessYouTubeLinksAsync(
            ApplicationDbContext context,
            int murderEventId,
            List<string> youtubeLinks,
            Func<string, string?> extractYouTubeIdFunc)
        {
            // Remove existing videos for this event
            var existingVideos = await context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToListAsync();
            context.MurderEventVideos.RemoveRange(existingVideos);

            // If no links provided or all links are empty, just save the deletion
            if (youtubeLinks == null || youtubeLinks.Count == 0)
            {
                if (existingVideos.Count > 0)
                    await context.SaveChangesAsync();
                return;
            }

            // Add up to MaxYouTubeVideos new videos
            var added = 0;
            foreach (var link in youtubeLinks)
            {
                if (added >= MaxYouTubeVideos)
                    break;

                if (string.IsNullOrWhiteSpace(link))
                    continue;

                var videoId = extractYouTubeIdFunc(link);
                if (videoId == null)
                    continue;

                context.MurderEventVideos.Add(new MurderEventVideo
                {
                    MurderEventId = murderEventId,
                    Url = link,
                    VideoId = videoId
                });

                added++;
            }

            // Always save if we removed existing videos or added new ones
            if (existingVideos.Count > 0 || added > 0)
                await context.SaveChangesAsync();
        }
    }
}
