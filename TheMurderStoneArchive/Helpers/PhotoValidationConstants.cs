namespace TheMurderStoneArchive.Helpers
{
    /// <summary>
    /// Centralized constants for photo upload validation.
    /// </summary>
    public static class PhotoValidationConstants
    {
        public const int MaxFiles = 10;
        public const long MaxFileSize = 25L * 1024L * 1024L; // 25 MB per file

        public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public const int MaxFileSizeMB = 25; // For display purposes
    }
}
