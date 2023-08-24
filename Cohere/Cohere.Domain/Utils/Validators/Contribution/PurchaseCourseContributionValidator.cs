using Cohere.Domain.Models.Payment;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class PurchaseCourseContributionValidator : AbstractValidator<PurchaseCourseContributionViewModel>
    {
        public PurchaseCourseContributionValidator()
        {
            RuleFor(c => c.PaymentOptions)
                .IsEnumName(typeof(PaymentOptions))
                .WithMessage("All the payment options must be one of predefined payment types");

            RuleFor(c => c.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} must not be empty.");

            When(c => c.PaymentOptions == PaymentOptions.SplitPayments.ToString(), () =>
            {
                RuleFor(c => c.PaymentMethodId)
                    .NotEmpty()
                    .WithMessage("{PropertyName} must not be empty.");
            });
        }
    }
}