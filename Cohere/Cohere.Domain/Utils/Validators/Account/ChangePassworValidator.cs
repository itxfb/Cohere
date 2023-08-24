using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class ChangePassworValidator : AbstractValidator<ChangePasswordViewModel>
    {
        public ChangePassworValidator()
        {
            RuleFor(a => a.Email).NotEmpty().WithMessage("{PropertyName} should not be empty");

            RuleFor(a => a.CurrentPassword).NotEmpty().WithMessage("{PropertyName} should not be empty");

            RuleFor(a => a.NewPassword).Cascade(CascadeMode.StopOnFirstFailure).NotNull().Matches("^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[!@#$%^&*])(?=.{8,})")
                .WithMessage("Password must contain at least 1 lowercase alphabetical character, at least 1 uppercase alphabetical character at least 1 numeric character, at least one special character, g must be eight characters or longer");
        }
    }
}
