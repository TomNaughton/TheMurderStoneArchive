using Moq;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TheMurderStoneArchive.Tests.Services
{
    /// <summary>
    /// Unit tests for MurderEventService YouTube ID extraction functionality.
    /// Tests use in-memory database to properly instantiate ApplicationDbContext.
    /// </summary>
    public class MurderEventServiceYouTubeTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private MurderEventService CreateService(ApplicationDbContext context = null)
        {
            var dbContext = context ?? CreateInMemoryContext();
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            return new MurderEventService(
                dbContext,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);
        }

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/v/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10s", "dQw4w9WgXcQ")]
        public void ExtractYouTubeId_WithValidUrls_ReturnsCorrectId(string url, string expectedId)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.ExtractYouTubeId(url);

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("https://www.google.com")]
        [InlineData("https://example.com/not-a-youtube-link")]
        [InlineData("https://vimeo.com/123456")]
        public void ExtractYouTubeId_WithInvalidUrls_ReturnsNull(string url)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.ExtractYouTubeId(url);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractYouTubeId_WithMalformedUrl_ReturnsNull()
        {
            // Arrange
            var service = CreateService();
            var malformedUrl = "ht!tp://[invalid";

            // Act
            var result = service.ExtractYouTubeId(malformedUrl);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExtractYouTubeId_WithWhitespaceOnlyUrl_ReturnsNull()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.ExtractYouTubeId("   ");

            // Assert
            Assert.Null(result);
        }
    }
}
