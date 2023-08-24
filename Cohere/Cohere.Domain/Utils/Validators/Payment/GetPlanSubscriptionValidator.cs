using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class GetPlanSubscriptionValidator : AbstractValidator<GetPlanSubscriptionViewModel>
    {
        public GetPlanSubscriptionValidator()
        {
            RuleFor(x => x.SubscriptionPlanId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.CustomerId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");
        }
    }
}
