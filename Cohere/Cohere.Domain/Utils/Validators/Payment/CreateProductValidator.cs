using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class CreateProductValidator : AbstractValidator<CreateProductViewModel>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Id).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.Name).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");
        }
    }
}
