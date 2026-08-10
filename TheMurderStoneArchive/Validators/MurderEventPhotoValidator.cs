using FluentValidation;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Validators
{
    public class MurderEventPhotoValidator : AbstractValidator<MurderEventPhoto>
    {
        public MurderEventPhotoValidator()
        {
            RuleFor(x => x.MurderEventId)
                .GreaterThan(0).WithMessage("A valid murder event must be associated with this photo.");

            RuleFor(x => x.FilePath)
                .NotEmpty().WithMessage("File path is required.")
                .MaximumLength(500).WithMessage("File path must not exceed 500 characters.");

            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("File name is required.")
                .MaximumLength(255).WithMessage("File name must not exceed 255 characters.");

            RuleFor(x => x.ContentType)
                .NotEmpty().WithMessage("Content type is required.")
                .Must(x => x.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Content type must be a valid image type (e.g., image/jpeg, image/png).");

            RuleFor(x => x.FileSize)
                .GreaterThan(0).WithMessage("File size must be greater than 0.")
                .LessThanOrEqualTo(10 * 1024 * 1024).WithMessage("File size must not exceed 10 MB.");
        }
    }
}
