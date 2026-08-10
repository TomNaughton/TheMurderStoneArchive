using Moq;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using Xunit;

namespace TheMurderStoneArchive.Tests.Helpers
{
    public class YouTubeVideoHelperTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithNullLinks_RemovesExistingVideosAndDoesNotAdd()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 1;

            // Add existing video
            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = murderEventId,
                Url = "https://www.youtube.com/watch?v=oldVideo",
                VideoId = "oldVideo"
            });
            await context.SaveChangesAsync();

            Func<string, string?> extractFn = (url) => "mockId";

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, null, extractFn);

            // Assert
            var remainingVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Empty(remainingVideos);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithEmptyList_RemovesExistingVideos()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 2;

            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = murderEventId,
                Url = "https://www.youtube.com/watch?v=oldVideo",
                VideoId = "oldVideo"
            });
            await context.SaveChangesAsync();

            var youtubeLinks = new List<string>();
            Func<string, string?> extractFn = (url) => "mockId";

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var remainingVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Empty(remainingVideos);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithValidLinks_AddsNewVideos()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 3;

            var youtubeLinks = new List<string>
            {
                "https://www.youtube.com/watch?v=video1",
                "https://www.youtube.com/watch?v=video2",
                "https://www.youtube.com/watch?v=video3"
            };

            Func<string, string?> extractFn = (url) => 
            {
                if (url.Contains("video1")) return "video1";
                if (url.Contains("video2")) return "video2";
                if (url.Contains("video3")) return "video3";
                return null;
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(3, addedVideos.Count);
            Assert.Contains(addedVideos, v => v.VideoId == "video1");
            Assert.Contains(addedVideos, v => v.VideoId == "video2");
            Assert.Contains(addedVideos, v => v.VideoId == "video3");
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithMoreThanThreeValidLinks_AddsOnlyThree()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 4;

            var youtubeLinks = new List<string>
            {
                "https://www.youtube.com/watch?v=video1",
                "https://www.youtube.com/watch?v=video2",
                "https://www.youtube.com/watch?v=video3",
                "https://www.youtube.com/watch?v=video4",
                "https://www.youtube.com/watch?v=video5"
            };

            var callCount = 0;
            Func<string, string?> extractFn = (url) => 
            {
                callCount++;
                return $"id{callCount}";
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(3, addedVideos.Count);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithWhitespaceOnlyLinks_SkipsThemAndAddsValidOnes()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 5;

            var youtubeLinks = new List<string>
            {
                "   ",
                "https://www.youtube.com/watch?v=validVideo",
                "",
                "https://www.youtube.com/watch?v=anotherVideo",
                "\t"
            };

            Func<string, string?> extractFn = (url) => 
            {
                if (url.Contains("validVideo")) return "validVideo";
                if (url.Contains("anotherVideo")) return "anotherVideo";
                return null;
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(2, addedVideos.Count);
            Assert.Contains(addedVideos, v => v.VideoId == "validVideo");
            Assert.Contains(addedVideos, v => v.VideoId == "anotherVideo");
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithInvalidVideoIds_SkipsThemAndAddsValidOnes()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 6;

            var youtubeLinks = new List<string>
            {
                "https://www.youtube.com/watch?v=invalidLink1",
                "https://www.youtube.com/watch?v=validVideo",
                "https://www.youtube.com/watch?v=invalidLink2",
                "https://www.youtube.com/watch?v=anotherValid"
            };

            Func<string, string?> extractFn = (url) => 
            {
                // Only accept URLs with "valid" or "another" in them
                if (url.Contains("validVideo")) return "validVideo";
                if (url.Contains("anotherValid")) return "anotherValid";
                return null; // Simulate invalid extraction
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(2, addedVideos.Count);
            Assert.Contains(addedVideos, v => v.VideoId == "validVideo");
            Assert.Contains(addedVideos, v => v.VideoId == "anotherValid");
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithExistingVideos_ReplacesThemWithNewOnes()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 7;

            // Add old videos
            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = murderEventId,
                Url = "https://www.youtube.com/watch?v=oldVideo1",
                VideoId = "oldVideo1"
            });
            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = murderEventId,
                Url = "https://www.youtube.com/watch?v=oldVideo2",
                VideoId = "oldVideo2"
            });
            await context.SaveChangesAsync();

            var newLinks = new List<string>
            {
                "https://www.youtube.com/watch?v=newVideo1",
                "https://www.youtube.com/watch?v=newVideo2"
            };

            Func<string, string?> extractFn = (url) => 
            {
                if (url.Contains("newVideo1")) return "newVideo1";
                if (url.Contains("newVideo2")) return "newVideo2";
                return null;
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, newLinks, extractFn);

            // Assert
            var videos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(2, videos.Count);
            Assert.DoesNotContain(videos, v => v.VideoId == "oldVideo1");
            Assert.DoesNotContain(videos, v => v.VideoId == "oldVideo2");
            Assert.Contains(videos, v => v.VideoId == "newVideo1");
            Assert.Contains(videos, v => v.VideoId == "newVideo2");
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithNoVideosAndNoExistingVideos_DoesNotSave()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 8;

            var youtubeLinks = new List<string>();
            Func<string, string?> extractFn = (url) => "mockId";

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Empty(addedVideos);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_PreservesUrlAndVideoIdCorrectly()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 9;
            const string testUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
            const string testVideoId = "dQw4w9WgXcQ";

            var youtubeLinks = new List<string> { testUrl };
            Func<string, string?> extractFn = (url) => testVideoId;

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideo = context.MurderEventVideos
                .FirstOrDefault(v => v.MurderEventId == murderEventId);

            Assert.NotNull(addedVideo);
            Assert.Equal(testUrl, addedVideo.Url);
            Assert.Equal(testVideoId, addedVideo.VideoId);
            Assert.Equal(murderEventId, addedVideo.MurderEventId);
        }

        [Fact]
        public async Task ProcessYouTubeLinksAsync_WithMixedValidAndInvalidLinks_AddsOnlyValidUpToMax()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const int murderEventId = 10;

            var youtubeLinks = new List<string>
            {
                "https://www.youtube.com/watch?v=valid1",
                "https://invalid.com/notayoutube",
                "https://www.youtube.com/watch?v=valid2",
                "https://notvalid.com/video",
                "https://www.youtube.com/watch?v=valid3",
                "https://www.youtube.com/watch?v=valid4" // Should be cut off
            };

            var validCount = 0;
            Func<string, string?> extractFn = (url) => 
            {
                if (url.Contains("valid1")) return "valid1";
                if (url.Contains("valid2")) return "valid2";
                if (url.Contains("valid3")) return "valid3";
                if (url.Contains("valid4")) return "valid4";
                return null; // Invalid URLs return null
            };

            // Act
            await YouTubeVideoHelper.ProcessYouTubeLinksAsync(context, murderEventId, youtubeLinks, extractFn);

            // Assert
            var addedVideos = context.MurderEventVideos
                .Where(v => v.MurderEventId == murderEventId)
                .ToList();
            Assert.Equal(3, addedVideos.Count); // Only 3 max
            Assert.DoesNotContain(addedVideos, v => v.VideoId == "valid4");
        }
    }
}
