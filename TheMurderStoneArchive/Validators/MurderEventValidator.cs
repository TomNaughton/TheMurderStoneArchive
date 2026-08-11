using FluentValidation;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Validators
{
    public class MurderEventValidator : AbstractValidator<MurderEvent>
    {
        public MurderEventValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .Length(1, 150).WithMessage("Title must be between 1 and 150 characters.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description is required.")
                .MinimumLength(10).WithMessage("Description must be at least 10 characters.");

            RuleFor(x => x.Year)
                .LessThanOrEqualTo(DateTime.UtcNow.Year)
                .WithMessage($"Year cannot be in the future (current year: {DateTime.UtcNow.Year}).");

            RuleFor(x => x.LocationId)
                .GreaterThan(0)
                .When(x => x.Location == null)
                .WithMessage("A valid location must be selected.");

            RuleFor(x => x.Location)
                .NotNull()
                .When(x => x.LocationId <= 0)
                .WithMessage("A location must be provided.");

            RuleFor(x => x.Location!.Name)
                .NotEmpty().WithMessage("Location name is required.")
                .When(x => x.Location != null);

            RuleFor(x => x.Category)
                .IsInEnum().WithMessage("Category must be a valid stone category.");
        }
    }
}
