using Cohere.Domain.Models.Payment;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class PurchaseOneToOneMonthlySessionSubscriptionValidator : AbstractValidator<PurchaseOneToOneMonthlySessionSubscriptionViewModel>
    {
        public PurchaseOneToOneMonthlySessionSubscriptionValidator()
        {
            RuleFor(c => c.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} must not be empty.");
        }
    }
}
