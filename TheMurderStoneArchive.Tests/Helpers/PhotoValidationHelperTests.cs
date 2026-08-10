using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TheMurderStoneArchive.Helpers;
using Xunit;

namespace TheMurderStoneArchive.Tests.Helpers
{
    public class PhotoValidationHelperTests
    {
        [Fact]
        public void ValidatePhotoFiles_WithNullList_ReturnsTrue()
        {
            // Arrange
            var modelState = new ModelStateDictionary();

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(null, modelState);

            // Assert
            Assert.True(result);
            Assert.Empty(modelState.Values.SelectMany(v => v.Errors));
        }

        [Fact]
        public void ValidatePhotoFiles_WithEmptyList_ReturnsTrue()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            var files = new List<IFormFile>();

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(files, modelState);

            // Assert
            Assert.True(result);
            Assert.Empty(modelState.Values.SelectMany(v => v.Errors));
        }

        [Fact]
        public void ValidatePhotoFiles_WithFilesExceedingMaxCount_ReturnsFalseAndAddsError()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            var files = new List<IFormFile>();

            // Create more files than allowed
            for (int i = 0; i <= PhotoValidationConstants.MaxFiles; i++)
            {
                var mockFile = new Mock<IFormFile>();
                mockFile.Setup(f => f.Length).Returns(1000);
                mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
                files.Add(mockFile.Object);
            }

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(files, modelState);

            // Assert
            Assert.False(result);
            Assert.Single(modelState.Values.SelectMany(v => v.Errors));
        }

        [Fact]
        public void ValidatePhotoFiles_WithFileTooLarge_ReturnsFalseAndAddsError()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(PhotoValidationConstants.MaxFileSize + 1000);
            mockFile.Setup(f => f.FileName).Returns("too-large.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

            var files = new List<IFormFile> { mockFile.Object };

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(files, modelState);

            // Assert
            Assert.False(result);
            Assert.Single(modelState.Values.SelectMany(v => v.Errors));
        }

        [Fact]
        public void ValidatePhotoFiles_WithInvalidContentType_ReturnsFalseAndAddsError()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1000);
            mockFile.Setup(f => f.FileName).Returns("document.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var files = new List<IFormFile> { mockFile.Object };

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(files, modelState);

            // Assert
            Assert.False(result);
            Assert.Single(modelState.Values.SelectMany(v => v.Errors));
        }

        [Fact]
        public void ValidatePhotoFiles_WithValidFiles_ReturnsTrue()
        {
            // Arrange
            var modelState = new ModelStateDictionary();
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(500000); // 500 KB
            mockFile.Setup(f => f.FileName).Returns("photo.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

            var files = new List<IFormFile> { mockFile.Object };

            // Act
            var result = PhotoValidationHelper.ValidatePhotoFiles(files, modelState);

            // Assert
            Assert.True(result);
            Assert.Empty(modelState.Values.SelectMany(v => v.Errors));
        }
    }
}
