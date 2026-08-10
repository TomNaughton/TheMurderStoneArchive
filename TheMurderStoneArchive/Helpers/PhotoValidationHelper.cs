using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TheMurderStoneArchive.Helpers
{
    /// <summary>
    /// Helper class for photo file validation logic.
    /// </summary>
    public static class PhotoValidationHelper
    {
        /// <summary>
        /// Validates a collection of photo files against size, type, and extension requirements.
        /// Adds ModelState errors for each validation failure.
        /// </summary>
        /// <param name="photos">Collection of files to validate</param>
        /// <param name="modelState">ModelStateDictionary to add errors to</param>
        /// <returns>True if all files are valid; false otherwise</returns>
        public static bool ValidatePhotoFiles(List<IFormFile>? photos, ModelStateDictionary modelState)
        {
            if (photos?.Count == 0 || photos is null)
                return true;

            if (photos.Count > PhotoValidationConstants.MaxFiles)
            {
                modelState.AddModelError("Photos", $"You may upload up to {PhotoValidationConstants.MaxFiles} photos.");
                return false;
            }

            bool hasErrors = false;

            foreach (var file in photos)
            {
                if (file.Length == 0)
                    continue;

                if (file.Length > PhotoValidationConstants.MaxFileSize)
                {
                    modelState.AddModelError("Photos", 
                        $"File {file.FileName} exceeds the {PhotoValidationConstants.MaxFileSizeMB} MB limit.");
                    hasErrors = true;
                    break;
                }

                if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    modelState.AddModelError("Photos", 
                        $"File {file.FileName} is not a valid image.");
                    hasErrors = true;
                    break;
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!PhotoValidationConstants.AllowedExtensions.Contains(ext))
                {
                    modelState.AddModelError("Photos", 
                        $"File {file.FileName} has an unsupported extension.");
                    hasErrors = true;
                    break;
                }
            }

            return !hasErrors;
        }
    }
}
