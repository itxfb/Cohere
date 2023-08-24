using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class SetClassCompletedValidator : AbstractValidator<SetClassAsCompletedViewModel>
    {
        public SetClassCompletedValidator()
        {
            Include(new SetAsCompletedValidator());

            RuleFor(c => c.ClassId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Class Id must not be empty")
                .MaximumLength(75).WithMessage("Class Id must not be longer than {MaxLength}");
        }
    }
}
