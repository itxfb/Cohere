using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class ProductSubscriptionValidator : AbstractValidator<ProductSubscriptionViewModel>
    {
        public ProductSubscriptionValidator()
        {
            RuleFor(x => x.StripeSubscriptionPlanId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.CustomerId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.DefaultPaymentMethod).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            When(e => e.Iterations.HasValue, () =>
            {
                RuleFor(x => x.Iterations).Cascade(CascadeMode.StopOnFirstFailure)
                    .GreaterThan(0);
            });
        }
    }
}
