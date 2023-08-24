using Cohere.Domain.Models.Payment;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.User
{
    public class BillingPlanValidator : AbstractValidator<PaidTierOptionViewModel>
    {
        public BillingPlanValidator()
        {
            // just for example, not accurate validation
            RuleFor(d => d.DisplayName).Cascade(CascadeMode.StopOnFirstFailure).NotEmpty()
                .WithMessage("{PropertyName} not empty").MaximumLength(150)
                .WithMessage("{PropertyName} maximum length is {MaxLength}");
        }
    }
}