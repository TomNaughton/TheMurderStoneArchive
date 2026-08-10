using FluentValidation;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Validators
{
    public class MurderEventVideoValidator : AbstractValidator<MurderEventVideo>
    {
        public MurderEventVideoValidator()
        {
            RuleFor(x => x.MurderEventId)
                .GreaterThan(0).WithMessage("A valid murder event must be associated with this video.");

            RuleFor(x => x.Url)
                .NotEmpty().WithMessage("Video URL is required.")
                .MaximumLength(2048).WithMessage("Video URL must not exceed 2048 characters.")
                .Must(x => Uri.TryCreate(x, UriKind.Absolute, out var result) && 
                           (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                .WithMessage("URL must be a valid HTTP or HTTPS URL.");

            RuleFor(x => x.VideoId)
                .MaximumLength(64).WithMessage("Video ID must not exceed 64 characters.")
                .Matches(@"^[a-zA-Z0-9_-]*$").When(x => !string.IsNullOrEmpty(x.VideoId))
                .WithMessage("Video ID must contain only alphanumeric characters, underscores, and hyphens.");
        }
    }
}
