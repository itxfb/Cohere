using Cohere.Domain.Models.Payment;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class PurchaseMembershipContributionValidator : AbstractValidator<PurchaseMembershipContributionViewModel>
    {
        public PurchaseMembershipContributionValidator()
        {
            RuleFor(c => c.PaymentOption)
                .NotEmpty().NotNull()
                .IsEnumName(typeof(PaymentOptions))
                .WithMessage("All the payment options must be one of predefined payment types");

            RuleFor(c => c.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} must not be empty.");
        }
    }
}