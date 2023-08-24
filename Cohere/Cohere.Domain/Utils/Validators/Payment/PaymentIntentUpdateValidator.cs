using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class PaymentIntentUpdateValidator : AbstractValidator<PaymentIntentUpdateViewModel>
    {
        public PaymentIntentUpdateValidator()
        {
            RuleFor(x => x.Id).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.Amount).Cascade(CascadeMode.StopOnFirstFailure)
                .GreaterThan(49)
                .WithMessage("USD currency minimum value is $0.50");
        }
    }
}
