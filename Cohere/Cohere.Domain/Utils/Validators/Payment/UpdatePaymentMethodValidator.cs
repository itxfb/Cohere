using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class UpdatePaymentMethodValidator : AbstractValidator<UpdatePaymentMethodViewModel>
    {
        public UpdatePaymentMethodValidator()
        {
            RuleFor(x => x.Id).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.PaymentMethodId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");
        }
    }
}
