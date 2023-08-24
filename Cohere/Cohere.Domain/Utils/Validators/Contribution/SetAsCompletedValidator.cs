using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class SetAsCompletedValidator : AbstractValidator<SetAsCompletedViewModel>
    {
        public SetAsCompletedValidator()
        {
            RuleFor(c => c.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id must not be empty")
                .MaximumLength(75).WithMessage("Contribution Id must not be longer than {MaxLength}");
        }
    }
}
