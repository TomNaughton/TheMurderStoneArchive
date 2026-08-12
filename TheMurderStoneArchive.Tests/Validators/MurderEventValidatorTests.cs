using Xunit;
using FluentValidation;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Validators;

namespace TheMurderStoneArchive.Tests.Validators
{
    public class MurderEventValidatorTests
    {
        private readonly MurderEventValidator _validator = new();

        [Fact]
        public void Validate_WithValidEvent_ReturnsNoErrors()
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = "The Headless Woman",
                Description = "A detailed account of the headless woman murder stone legend in Somerset.",
                Year = 1700,
                LocationId = 1,
                Category = StoneCategory.Confirmed
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_WithMissingTitle_ReturnsError()
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = "",
                Description = "A valid description here",
                Year = 1700,
                LocationId = 1
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "Title" && 
                e.ErrorMessage.Contains("required")));
        }

        [Fact]
        public void Validate_WithTitleTooLong_ReturnsError()
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = new string('a', 151),
                Description = "A valid description here",
                Year = 1700,
                LocationId = 1
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("between 1 and 150"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("short")]
        public void Validate_WithShortDescription_ReturnsError(string description)
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = "Valid Title",
                Description = description,
                Year = 1700,
                LocationId = 1
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "Description"));
        }

        [Fact]
        public void Validate_WithFutureYear_ReturnsError()
        {
            // Arrange
            var futureYear = DateTime.UtcNow.Year + 1;
            var @event = new MurderEvent
            {
                Title = "Valid Title",
                Description = "A valid description here",
                Year = futureYear,
                LocationId = 1
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "Year"));
        }

        [Fact]
        public void Validate_WithMissingLocationId_ReturnsError()
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = "Valid Title",
                Description = "A valid description here",
                Year = 1700,
                LocationId = 0
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "LocationId"));
        }

        [Fact]
        public void Validate_WithValidYear_ReturnsNoError()
        {
            // Arrange
            var @event = new MurderEvent
            {
                Title = "Valid Title",
                Description = "A valid description here",
                Year = 1500,
                LocationId = 1
            };

            // Act
            var result = _validator.Validate(@event);

            // Assert
            Assert.True(result.IsValid);
        }
    }
}
