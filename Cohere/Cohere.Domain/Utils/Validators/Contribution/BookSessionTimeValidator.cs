using Cohere.Domain.Models.ContributionViewModels.ForClient;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class BookSessionTimeValidator : AbstractValidator<BookSessionTimeViewModel>
    {
        public BookSessionTimeValidator()
        {
            RuleFor(c => c.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id must not be empty")
                .MaximumLength(70).WithMessage("Contribution Id must not be longer than {MaxLength}");

            RuleFor(c => c.SessionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Session Id must not be empty")
                .MaximumLength(70).WithMessage("Session Id must not be longer than {MaxLength}");

            RuleFor(c => c.SessionTimeId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Session time Id must not be empty")
                .MaximumLength(70).WithMessage("Session time Id must not be longer than {MaxLength}");
        }
    }
}
