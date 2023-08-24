using Cohere.Domain.Models.Payment;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class PurchaseOneToOnePackageValidator : AbstractValidator<PurchaseOneToOnePackageViewModel>
    {
        public PurchaseOneToOnePackageValidator()
        {
            RuleFor(c => c.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} must not be empty.");
        }
    }
}
