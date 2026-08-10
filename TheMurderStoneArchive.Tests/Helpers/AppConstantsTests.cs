using TheMurderStoneArchive.Helpers;
using Xunit;

namespace TheMurderStoneArchive.Tests.Helpers
{
    public class AppConstantsTests
    {
        [Fact]
        public void Pagination_Constants_AreValid()
        {
            // Assert values are reasonable
            Assert.Equal(10, AppConstants.DefaultPageSize);
            Assert.Equal(1, AppConstants.DefaultPage);
            Assert.True(AppConstants.DefaultPageSize > 0);
        }

        [Fact]
        public void SortOrder_Constants_AreNotEmpty()
        {
            // All sort order constants should be defined
            Assert.NotEmpty(AppConstants.SortOrderTitle);
            Assert.NotEmpty(AppConstants.SortOrderTitleDesc);
            Assert.NotEmpty(AppConstants.SortOrderYearAsc);
            Assert.NotEmpty(AppConstants.SortOrderYearDesc);
            Assert.NotEmpty(AppConstants.SortOrderLocation);
        }

        [Fact]
        public void Configuration_Constants_AreProperlyFormatted()
        {
            // Configuration keys should follow proper formatting
            Assert.Equal("ReCaptcha:SiteKey", AppConstants.ReCaptchaSiteKeyKey);
            Assert.Equal("ReCaptcha:SecretKey", AppConstants.ReCaptchaSecretKeyKey);
            Assert.Equal("DefaultConnection", AppConstants.ConnectionStringKey);
        }

        [Fact]
        public void YouTube_Constants_AreValid()
        {
            // YouTube constants should be meaningful
            Assert.NotEmpty(AppConstants.YouTubeHost);
            Assert.NotEmpty(AppConstants.YouTubeShortHost);
            Assert.NotEmpty(AppConstants.YouTubeEmbedPath);
            Assert.NotEmpty(AppConstants.YouTubeVideoParamKey);
            Assert.NotEmpty(AppConstants.YouTubeIdRegexPattern);
            Assert.True(AppConstants.YouTubeIdMinLength > 0);
        }

        [Fact]
        public void ReCaptcha_Constants_AreValid()
        {
            // ReCaptcha constants should be reasonable
            Assert.Equal(0.5, AppConstants.ReCaptchaDefaultMinScore);
            Assert.True(AppConstants.ReCaptchaTimeoutSeconds > 0);
            Assert.NotEmpty(AppConstants.ReCaptchaVerifyUrl);
            Assert.StartsWith("https://", AppConstants.ReCaptchaVerifyUrl);
        }

        [Fact]
        public void Role_AdminRole_IsCorrect()
        {
            Assert.Equal("Admin", AppConstants.AdminRole);
        }
    }
}
