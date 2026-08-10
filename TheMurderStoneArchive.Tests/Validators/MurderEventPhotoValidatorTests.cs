using Xunit;
using FluentValidation;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Validators;

namespace TheMurderStoneArchive.Tests.Validators
{
    public class MurderEventPhotoValidatorTests
    {
        private readonly MurderEventPhotoValidator _validator = new();

        [Fact]
        public void Validate_WithValidPhoto_ReturnsNoErrors()
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/murder-stone.jpg",
                FileName = "murder-stone.jpg",
                ContentType = "image/jpeg",
                FileSize = 1024 * 50 // 50 KB
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_WithMissingMurderEventId_ReturnsError()
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 0,
                FilePath = "/photos/murder-stone.jpg",
                FileName = "murder-stone.jpg",
                ContentType = "image/jpeg",
                FileSize = 1024 * 50
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "MurderEventId"));
        }

        [Fact]
        public void Validate_WithMissingFilePath_ReturnsError()
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "",
                FileName = "murder-stone.jpg",
                ContentType = "image/jpeg",
                FileSize = 1024 * 50
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "FilePath"));
        }

        [Fact]
        public void Validate_WithInvalidContentType_ReturnsError()
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/document.pdf",
                FileName = "document.pdf",
                ContentType = "application/pdf",
                FileSize = 1024 * 50
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "ContentType" && 
                e.ErrorMessage.Contains("image")));
        }

        [Fact]
        public void Validate_WithFileTooLarge_ReturnsError()
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/huge.jpg",
                FileName = "huge.jpg",
                ContentType = "image/jpeg",
                FileSize = 11 * 1024 * 1024 // 11 MB
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.False(result.IsValid);
            Assert.True(result.Errors.Any(e => e.PropertyName == "FileSize" && 
                e.ErrorMessage.Contains("10 MB")));
        }

        [Theory]
        [InlineData("image/jpeg")]
        [InlineData("image/png")]
        [InlineData("image/webp")]
        [InlineData("image/gif")]
        public void Validate_WithValidImageContentTypes_ReturnsNoErrors(string contentType)
        {
            // Arrange
            var photo = new MurderEventPhoto
            {
                MurderEventId = 1,
                FilePath = "/photos/image.jpg",
                FileName = "image.jpg",
                ContentType = contentType,
                FileSize = 1024 * 50
            };

            // Act
            var result = _validator.Validate(photo);

            // Assert
            Assert.True(result.IsValid);
        }
    }
}
