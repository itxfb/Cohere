using Cohere.Domain.Models.ContributionViewModels.Shared;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class UnfinishedContributionBaseValidator : AbstractValidator<ContributionBaseViewModel>
    {
        public UnfinishedContributionBaseValidator()
        {
            RuleFor(c => c.UserId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("User Id must not be empty")
                .MaximumLength(100).WithMessage("User Id maximum length is {MaxLength}");

            RuleFor(c => c.Title).Cascade(CascadeMode.StopOnFirstFailure)
               .NotEmpty().WithMessage("{PropertyName} must not be empty")
               .MaximumLength(50).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(c => c.Categories).NotEmpty()
                .WithMessage("{PropertyName} must be not empty");
        }
    }
}
