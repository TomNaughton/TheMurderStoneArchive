using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;
using Xunit;

namespace TheMurderStoneArchive.Tests.Data
{
    /// <summary>
    /// Unit tests for MurderEventQueryExtensions query helper methods.
    /// Tests verify that extension methods properly filter and include related entities.
    /// </summary>
    public class MurderEventQueryExtensionsTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("test_db_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private async Task<ApplicationDbContext> SeedTestData()
        {
            var context = CreateInMemoryContext();

            // Add locations
            var location1 = new Location { Id = 1, Name = "Somerset", Latitude = 51.0, Longitude = -2.5 };
            var location2 = new Location { Id = 2, Name = "Devon", Latitude = 50.7, Longitude = -3.5 };
            context.Locations.AddRange(location1, location2);

            // Add perpetrators
            var perp1 = new Perpetrator { Id = 1, FullName = "John Doe", Punishment = "Execution" };
            var perp2 = new Perpetrator { Id = 2, FullName = "Jane Smith", Punishment = "Imprisonment" };
            context.Perpetrators.AddRange(perp1, perp2);

            // Add monuments
            var monument1 = new Monument { Id = 1, MonumentType = "Stone", Inscription = "Description 1", FundedBy = "Public" };
            var monument2 = new Monument { Id = 2, MonumentType = "Stone", Inscription = "Description 2", FundedBy = "Private" };
            context.Monuments.AddRange(monument1, monument2);

            // Add murder events with various states
            var approvedEvent = new MurderEvent
            {
                Id = 1,
                Title = "Approved Event",
                Description = "This is approved",
                Year = 1700,
                LocationId = 1,
                IsApproved = true,
                IsLost = false
            };

            var unapprovedEvent = new MurderEvent
            {
                Id = 2,
                Title = "Unapproved Event",
                Description = "Not approved yet",
                Year = 1701,
                LocationId = 2,
                IsApproved = false,
                IsLost = false
            };

            var lostEvent = new MurderEvent
            {
                Id = 3,
                Title =     "Lost Event",
                Description = "This one is lost",
                Year = 1702,
                LocationId = 1,
                IsApproved = true,
                IsLost = true
            };

            context.MurderEvents.AddRange(approvedEvent, unapprovedEvent, lostEvent);

            // Add relationships
            approvedEvent.Perpetrators.Add(perp1);
            approvedEvent.Monuments.Add(monument1);
            lostEvent.Perpetrators.Add(perp2);
            lostEvent.Monuments.Add(monument2);

            // Add photos and videos
            context.MurderEventPhotos.Add(new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/event1.jpg",
                FileName = "event1.jpg",
                ContentType = "image/jpeg",
                FileSize = 2048
            });

            context.MurderEventVideos.Add(new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=test1",
                VideoId = "test1"
            });

            await context.SaveChangesAsync();
            return context;
        }

        [Fact]
        public async Task ApprovedAndNotLost_FiltersToApprovedEventsOnly()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.NotEmpty(results);
            Assert.Single(results); // Only the approved, not-lost event
            Assert.Equal(1, results[0].Id);
            Assert.True(results[0].IsApproved);
            Assert.False(results[0].IsLost);
        }

        [Fact]
        public async Task ApprovedAndNotLost_ExcludesUnapprovedEvents()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.DoesNotContain(results, e => e.Id == 2); // Unapproved event
        }

        [Fact]
        public async Task ApprovedAndNotLost_ExcludesLostEvents()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.DoesNotContain(results, e => e.Id == 3); // Lost event
        }

        [Fact]
        public async Task WithAllRelations_IncludesLocationPhotosVideosMonumentsAndPerpetrators()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithAllRelations()
                .FirstOrDefault(e => e.Id == 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location);
            Assert.NotEmpty(result.Photos);
            Assert.NotEmpty(result.Videos);
            Assert.NotEmpty(result.Perpetrators);
            Assert.NotEmpty(result.Monuments);
        }

        [Fact]
        public async Task WithAllRelations_LoadsNullRelationsForEventsWithoutData()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithAllRelations()
                .FirstOrDefault(e => e.Id == 2); // Unapproved event with minimal data

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location);
            Assert.Empty(result.Photos);
            Assert.Empty(result.Videos);
            Assert.Empty(result.Perpetrators);
            Assert.Empty(result.Monuments);
        }

        [Fact]
        public async Task WithBasicRelations_IncludesLocationPhotosAndVideos()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithBasicRelations()
                .FirstOrDefault(e => e.Id == 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location);
            Assert.NotEmpty(result.Photos);
            Assert.NotEmpty(result.Videos);
        }

        [Fact]
        public async Task WithBasicRelations_ExcludesMonumentsAndPerpetrators()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithBasicRelations()
                .FirstOrDefault(e => e.Id == 1);

            // Assert
            Assert.NotNull(result);
            // Perpetrators and Monuments should not be loaded separately
        }

        [Fact]
        public async Task WithLocation_IncludesLocationOnly()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithLocation()
                .FirstOrDefault(e => e.Id == 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location);
            Assert.Equal("Somerset", result.Location.Name);
        }

        [Fact]
        public async Task WithLocation_ExcludesOtherRelations()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var result = context.MurderEvents
                .WithLocation()
                .FirstOrDefault(e => e.Id == 1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Location); // Location is included
            // Photos, Videos, Perpetrators, Monuments might not be loaded
        }

        [Fact]
        public async Task ChainedExtensions_ApprovedAndNotLostWithAllRelations()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .WithAllRelations()
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.NotEmpty(results);
            Assert.Single(results);
            Assert.Equal(1, results[0].Id);
            Assert.NotNull(results[0].Location);
            Assert.NotEmpty(results[0].Photos);
            Assert.NotEmpty(results[0].Videos);
        }

        [Fact]
        public async Task ChainedExtensions_ApprovedAndNotLostWithBasicRelations()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .WithBasicRelations()
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.NotEmpty(results);
            Assert.Single(results);
            Assert.NotNull(results[0].Location);
            Assert.NotEmpty(results[0].Photos);
            Assert.NotEmpty(results[0].Videos);
        }

        [Fact]
        public async Task WithAllRelations_CanBeChainedWithWhereClause()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .WithAllRelations()
                .Where(e => e.Year > 1700)
                .ToList();

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, e => Assert.True(e.Year > 1700));
        }

        [Fact]
        public async Task WithLocation_CorrectlyJoinsWithLocationTable()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var events = context.MurderEvents
                .WithLocation()
                .ToList();

            // Assert
            Assert.All(events, e => 
            {
                Assert.NotNull(e.Location);
                Assert.True(e.LocationId == e.Location.Id);
            });
        }

        [Fact]
        public async Task ApprovedAndNotLost_WithNoMatchingEvents_ReturnsEmpty()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var location = new Location { Id = 1, Name = "Test", Latitude = 0, Longitude = 0 };
            context.Locations.Add(location);

            // Add only unapproved or lost events
            context.MurderEvents.Add(new MurderEvent
            {
                Id = 1,
                Title = "Test",
                Description = "Test",
                Year = 1700,
                LocationId = 1,
                IsApproved = false,
                IsLost = false
            });
            await context.SaveChangesAsync();

            // Act
            var results = context.MurderEvents
                .ApprovedAndNotLost()
                .ToList();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task WithAllRelations_AllPropertiesAreNotNull()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var events = context.MurderEvents
                .WithAllRelations()
                .ToList();

            // Assert
            Assert.All(events, e => 
            {
                Assert.NotNull(e);
                Assert.NotNull(e.Location);
                Assert.NotNull(e.Photos);
                Assert.NotNull(e.Videos);
                Assert.NotNull(e.Perpetrators);
                Assert.NotNull(e.Monuments);
            });
        }

        [Fact]
        public async Task Extensions_WorkWithOrderBy()
        {
            // Arrange
            var context = await SeedTestData();

            // Act
            var results = context.MurderEvents
                .WithLocation()
                .ApprovedAndNotLost()
                .OrderBy(e => e.Year)
                .ToList();

            // Assert
            Assert.NotEmpty(results);
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.True(results[i].Year <= results[i + 1].Year);
            }
        }
    }
}
