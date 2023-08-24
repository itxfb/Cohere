using Cohere.Domain.Models.Payment.Stripe;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Payment
{
    public class CreateProductPlanValidator : AbstractValidator<CreateProductPlanViewModel>
    {
        public CreateProductPlanValidator()
        {
            RuleFor(x => x.Interval).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.Name).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.ProductId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty");

            RuleFor(x => x.Amount).Cascade(CascadeMode.StopOnFirstFailure)
                .GreaterThan(49)
                .WithMessage("USD currency minimum value is $0.50");

            When(e => e.SplitNumbers.HasValue, () =>
            {
                RuleFor(x => x.SplitNumbers).Cascade(CascadeMode.StopOnFirstFailure)
                    .GreaterThan(1);
            });
        }
    }
}
