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
                .GreaterThanOrEqualTo(1400)
                .WithMessage("Year must be 1400 or later.")
                .LessThanOrEqualTo(DateTime.UtcNow.Year)
                .WithMessage($"Year cannot be in the future (current year: {DateTime.UtcNow.Year}).");

            RuleFor(x => x.LocationId)
                .GreaterThan(0).WithMessage("A valid location must be selected.");

            RuleFor(x => x.Category)
                .IsInEnum().WithMessage("Category must be a valid stone category.");
        }
    }
}
