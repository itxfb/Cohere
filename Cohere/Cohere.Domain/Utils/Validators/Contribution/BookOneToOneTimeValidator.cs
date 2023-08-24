using Cohere.Domain.Models.ContributionViewModels.ForClient;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class BookOneToOneTimeValidator : AbstractValidator<BookOneToOneTimeViewModel>
    {
        public BookOneToOneTimeValidator()
        {
            RuleFor(c => c.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id must not be empty")
                .MaximumLength(50).WithMessage("Contribution Id must not be longer than {MaxLength}");

            RuleFor(c => c.AvailabilityTimeId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Availability Time Id must not be empty")
                .MaximumLength(50).WithMessage("Availability Time Id must not be longer than {MaxLength}");
        }
    }
}
