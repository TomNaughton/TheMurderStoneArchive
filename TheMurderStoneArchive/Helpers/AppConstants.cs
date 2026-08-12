namespace TheMurderStoneArchive.Helpers
{
    /// <summary>
    /// Application-wide constants for magic strings and configuration values.
    /// </summary>
    public static class AppConstants
    {
        // Pagination
        public const int DefaultPageSize = 10;
        public const int DefaultPage = 1;

        // Sorting
        public const string SortOrderTitle = "title";
        public const string SortOrderTitleDesc = "title_desc";
        public const string SortOrderYearAsc = "year_asc";
        public const string SortOrderYearDesc = "year_desc";
        public const string SortOrderLocation = "location";

        // Configuration Keys
        public const string ReCaptchaSection = "ReCaptcha";
        public const string ReCaptchaSiteKeyKey = "ReCaptcha:SiteKey";
        public const string ReCaptchaSecretKeyKey = "ReCaptcha:SecretKey";
        public const string ConnectionStringKey = "DefaultConnection";

        // ReCAPTCHA
        public const double ReCaptchaDefaultMinScore = 0.5;
        public const string ReCaptchaVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";
        public const int ReCaptchaTimeoutSeconds = 5;

        // YouTube
        public const string YouTubeHost = "youtube.com";
        public const string YouTubeShortHost = "youtu.be";
        public const string YouTubeEmbedPath = "/embed/";
        public const string YouTubeVideoParamKey = "v";
        public const string YouTubeIdRegexPattern = @"(?:v=|/v/|/embed/|youtu\.be/)([A-Za-z0-9_-]{6,})";
        public const int YouTubeIdMinLength = 6;

        // Roles
        public const string AdminRole = "Admin";

        // File Paths & Uploads
        public const string UploadsDirectory = "wwwroot/uploads";
        public const string MurderEventsUploadDir = "murderevents";

        // Database
        public const string DefaultDatabaseName = "MurderStoneArchiveDb";

        // API
        public const string CamelCaseJsonPropertyNaming = "CamelCase";
        public const int MaxFreeApiKeysPerUser = 3;
        public const int MaxPremiumApiKeysPerUser = 1;

        // HTTP
        public const string DefaultHttpClientName = "default";

        // Stripe
        public const string StripeSection = "Stripe";

        // Donations
        public const string DonationSection = "Donation";
    }
}
