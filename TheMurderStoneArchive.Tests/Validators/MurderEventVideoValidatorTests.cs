using Xunit;
using FluentValidation;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Validators;

namespace TheMurderStoneArchive.Tests.Validators
{
    public class MurderEventVideoValidatorTests
    {
        private readonly MurderEventVideoValidator _validator = new();

        [Fact]
        public void Validate_WithValidYouTubeUrl_ReturnsNoErrors()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                VideoId = "dQw4w9WgXcQ"
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_WithMissingMurderEventId_ReturnsError()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 0,
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "MurderEventId"));
        }

        [Fact]
        public void Validate_WithMissingUrl_ReturnsError()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = ""
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "Url"));
        }

        [Theory]
        [InlineData("not a url")]
        [InlineData("ftp://example.com")]
        [InlineData("htp://invalid-protocol.com")]
        public void Validate_WithInvalidUrl_ReturnsError(string url)
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = url
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "Url"));
        }

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ")]
        [InlineData("http://youtube.com/embed/dQw4w9WgXcQ")]
        public void Validate_WithValidVideoUrls_ReturnsNoErrors(string url)
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = url
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithValidVideoId_ReturnsNoErrors()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=abc123",
                VideoId = "abc123"
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithInvalidVideoId_ReturnsError()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=invalid",
                VideoId = "invalid!@#$"
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "VideoId"));
        }

        [Fact]
        public void Validate_WithNullVideoId_ReturnsNoErrors()
        {
            // Arrange
            var video = new MurderEventVideo
            {
                MurderEventId = 1,
                Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                VideoId = null
            };

            // Act
            var result = _validator.Validate(video);

            // Assert
            Assert.True(result.IsValid);
        }
    }
}
