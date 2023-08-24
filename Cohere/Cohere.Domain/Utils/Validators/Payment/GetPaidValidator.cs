using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class GetPaidValidator : AbstractValidator<GetPaidViewModel>
    {
        public GetPaidValidator()
        {
            RuleFor(x => x.Amount).Cascade(CascadeMode.StopOnFirstFailure)
                .GreaterThan(0.49m)
                .WithMessage("USD currency minimum value is $0.50");
        }
    }
}
