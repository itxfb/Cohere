using Cohere.Domain.Models.ContributionViewModels;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.EmailMessages
{
    public class ShareContributionEmailModelValidator : AbstractValidator<ShareContributionEmailViewModel>
    {
        public ShareContributionEmailModelValidator()
        {
            RuleFor(c => c.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id must not be empty")
                .MaximumLength(100).WithMessage("Contribution Id must not be longer than {MaxLength}");

            RuleFor(c => c.EmailAddresses).Cascade(CascadeMode.StopOnFirstFailure)
                .NotNull()
                .NotEmpty();

            When(c => c.EmailAddresses.Count > 0, () =>
                RuleForEach(c => c.EmailAddresses).Cascade(CascadeMode.StopOnFirstFailure)
                    .NotEmpty().WithMessage("Email address must not be empty string")
                    .EmailAddress().WithMessage("Email address {PropertyValue} must be valid email")
                    .MaximumLength(150).WithMessage("Email address {PropertyValue} must not be longer than {MaxLength}"));
        }
    }
}
