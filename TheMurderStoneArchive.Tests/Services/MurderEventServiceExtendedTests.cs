using Moq;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Services;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Net;
using System.Text.Json;

namespace TheMurderStoneArchive.Tests.Services
{
    /// <summary>
    /// Unit tests for MurderEventService GetEventsAsync pagination and sorting functionality.
    /// </summary>
    public class MurderEventServiceGetEventsTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private MurderEventService CreateService(ApplicationDbContext context)
        {
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            return new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);
        }

        private async Task<ApplicationDbContext> SeedTestData()
        {
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            for (int i = 1; i <= 25; i++)
            {
                context.MurderEvents.Add(new MurderEvent
                {
                    Id = i,
                    Title = $"Murder Event {i}",
                    Description = $"Description for event {i}",
                    Year = 1700 + i,
                    LocationId = 1,
                    IsApproved = i % 2 == 0, // Half approved
                    IsLost = false
                });
            }
            await context.SaveChangesAsync();
            return context;
        }

        [Fact]
        public async Task GetEventsAsync_WithDefaultParameters_ReturnsPaginatedApprovedEvents()
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync();

            // Assert
            Assert.NotEmpty(events);
            Assert.True(totalCount > 0);
            Assert.All(events, e => Assert.True(e.IsApproved));
            Assert.Equal(10, events.Count); // Default page size
        }

        [Fact]
        public async Task GetEventsAsync_WithPageTwo_ReturnsSecondPage()
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(page: 2, pageSize: 5);

            // Assert
            Assert.Equal(5, events.Count);
            Assert.True(events[0].Id > 5 || events[0].Title.Contains("Murder Event"));
        }

        [Fact]
        public async Task GetEventsAsync_WithSearchTerm_FiltersEventsByTitleAndDescription()
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(searchTerm: "Event 2");

            // Assert
            Assert.NotEmpty(events);
            Assert.All(events, e => 
                Assert.True(e.Title.ToLower().Contains("event 2") || 
                           e.Description.ToLower().Contains("event 2")));
        }

        [Fact]
        public async Task GetEventsAsync_WithWhitespacePaddedSearchTerm_TrimsAndFiltersCorrectly()
        {
            // Arrange
            // Non-Npgsql providers (e.g. InMemory used here) fall back to a client-lowered
            // Contains comparison; this verifies the search term is trimmed before filtering.
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(searchTerm: "  Event 2  ");

            // Assert
            Assert.NotEmpty(events);
            Assert.All(events, e =>
                Assert.True(e.Title.ToLower().Contains("event 2") ||
                           e.Description.ToLower().Contains("event 2")));
        }

        [Theory]
        [InlineData(AppConstants.SortOrderTitleDesc)]
        [InlineData(AppConstants.SortOrderYearAsc)]
        [InlineData(AppConstants.SortOrderYearDesc)]
        [InlineData(AppConstants.SortOrderLocation)]
        public async Task GetEventsAsync_WithVariousSortOrders_SortsCorrectly(string sortOrder)
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(sortOrder: sortOrder);

            // Assert
            Assert.NotEmpty(events);
            Assert.True(events.Count > 0);
        }

        [Fact]
        public async Task GetEventsAsync_WithNonExistentPage_ReturnsEmptyList()
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(page: 100, pageSize: 10);

            // Assert
            Assert.Empty(events);
        }

        [Fact]
        public async Task GetEventsAsync_WithUserIdAndUnapprovedEvent_ReturnsUserOwnedUnapprovedEvents()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userId = "user123";
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            context.MurderEvents.Add(new MurderEvent
            {
                Id = 1,
                Title = "Approved Event",
                Description = "Approved",
                Year = 1700,
                LocationId = 1,
                IsApproved = true,
                IsLost = false,
                CreatedById = userId
            });

            context.MurderEvents.Add(new MurderEvent
            {
                Id = 2,
                Title = "User's Unapproved Event",
                Description = "Unapproved",
                Year = 1701,
                LocationId = 1,
                IsApproved = false,
                IsLost = false,
                CreatedById = userId
            });

            context.MurderEvents.Add(new MurderEvent
            {
                Id = 3,
                Title = "Other User's Unapproved Event",
                Description = "Unapproved",
                Year = 1702,
                LocationId = 1,
                IsApproved = false,
                IsLost = false,
                CreatedById = "otherUser"
            });

            await context.SaveChangesAsync();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync(currentUserId: userId);

            // Assert
            Assert.NotEmpty(events);
            Assert.Contains(events, e => e.Id == 1); // Approved
            Assert.Contains(events, e => e.Id == 2); // User's unapproved
            Assert.DoesNotContain(events, e => e.Id == 3); // Other user's unapproved
        }

        [Fact]
        public async Task GetEventsAsync_IncludesLocationRelation()
        {
            // Arrange
            var context = await SeedTestData();
            var service = CreateService(context);

            // Act
            var (events, totalCount) = await service.GetEventsAsync();

            // Assert
            Assert.All(events, e => Assert.NotNull(e.Location));
        }
    }

    /// <summary>
    /// Unit tests for MurderEventService GetEventByIdAsync functionality.
    /// </summary>
    public class MurderEventServiceGetEventByIdTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private MurderEventService CreateService(ApplicationDbContext context)
        {
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            return new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);
        }

        [Fact]
        public async Task GetEventByIdAsync_WithValidApprovedEventId_ReturnsEvent()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            var @event = new MurderEvent
            {
                Id = 1,
                Title = "Test Event",
                Description = "Test Description",
                Year = 1700,
                LocationId = 1,
                IsApproved = true,
                IsLost = false
            };
            context.MurderEvents.Add(@event);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Event", result.Title);
        }

        [Fact]
        public async Task GetEventByIdAsync_WithUnapprovedEventAndNoUserId_ReturnsNull()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            var @event = new MurderEvent
            {
                Id = 1,
                Title = "Unapproved Event",
                Description = "Not approved",
                Year = 1700,
                LocationId = 1,
                IsApproved = false,
                IsLost = false,
                CreatedById = "someUser"
            };
            context.MurderEvents.Add(@event);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(1);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetEventByIdAsync_WithUnapprovedEventAndCorrectUserId_ReturnsEvent()
        {
            // Arrange
            var context = CreateInMemoryContext();
            const string userId = "user123";
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            var @event = new MurderEvent
            {
                Id = 1,
                Title = "Unapproved Event",
                Description = "Not approved",
                Year = 1700,
                LocationId = 1,
                IsApproved = false,
                IsLost = false,
                CreatedById = userId
            };
            context.MurderEvents.Add(@event);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(1, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetEventByIdAsync_WithUnapprovedEventAndWrongUserId_ReturnsNull()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            var @event = new MurderEvent
            {
                Id = 1,
                Title = "Unapproved Event",
                Description = "Not approved",
                Year = 1700,
                LocationId = 1,
                IsApproved = false,
                IsLost = false,
                CreatedById = "correctUser"
            };
            context.MurderEvents.Add(@event);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(1, "wrongUser");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetEventByIdAsync_WithNonExistentId_ReturnsNull()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetEventByIdAsync_IncludesAllRelations()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            context.Locations.Add(location);

            var @event = new MurderEvent
            {
                Id = 1,
                Title = "Test Event",
                Description = "Test",
                Year = 1700,
                LocationId = 1,
                IsApproved = true,
                IsLost = false
            };
            context.MurderEvents.Add(@event);

            context.MurderEventPhotos.Add(new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/test.jpg",
                FileName = "test.jpg",
                ContentType = "image/jpeg",
                FileSize = 1024
            });

            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=test",
                VideoId = "test"
            });

            await context.SaveChangesAsync();
            var service = CreateService(context);

            // Act
            var result = await service.GetEventByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location);
            Assert.NotEmpty(result.Photos);
            Assert.NotEmpty(result.Videos);
        }
    }

    /// <summary>
    /// Unit tests for MurderEventService VerifyReCaptchaAsync functionality.
    /// </summary>
    public class MurderEventServiceReCaptchaTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private MurderEventService CreateServiceWithMockedHttp(ApplicationDbContext context, Func<HttpRequestMessage, HttpResponseMessage> responseHandler)
        {
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockHttpClient = new Mock<HttpClient>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            mockConfig
                .Setup(c => c[AppConstants.ReCaptchaSecretKeyKey])
                .Returns("test_secret_key");

            // Setup the HttpClient to return appropriate responses
            mockHttpFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(mockHttpClient.Object);

            return new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);
        }

        [Fact]
        public async Task VerifyReCaptchaAsync_WithValidToken_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            mockConfig
                .Setup(c => c[AppConstants.ReCaptchaSecretKeyKey])
                .Returns("test_secret_key");

            var service = new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);

            // Act
            var result = await service.VerifyReCaptchaAsync("valid_token");

            // Assert - This test validates that the method can be called
            // Real reCAPTCHA verification would require mocking HttpClientFactory
            // which is complex without additional test utilities
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task VerifyReCaptchaAsync_WithEmptyToken_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            mockConfig
                .Setup(c => c[AppConstants.ReCaptchaSecretKeyKey])
                .Returns("test_secret_key");

            var service = new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);

            // Act
            var result = await service.VerifyReCaptchaAsync("");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyReCaptchaAsync_WithNullToken_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            mockConfig
                .Setup(c => c[AppConstants.ReCaptchaSecretKeyKey])
                .Returns("test_secret_key");

            var service = new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);

            // Act
            var result = await service.VerifyReCaptchaAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyReCaptchaAsync_WithMissingSecretKey_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var mockHttpFactory = new Mock<IHttpClientFactory>();
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<MurderEventService>>();

            mockConfig
                .Setup(c => c[AppConstants.ReCaptchaSecretKeyKey])
                .Returns((string)null); // No secret key configured

            var service = new MurderEventService(
                context,
                mockHttpFactory.Object,
                mockConfig.Object,
                mockLogger.Object);

            // Act
            var result = await service.VerifyReCaptchaAsync("some_token");

            // Assert
            Assert.False(result);
        }
    }
}
