using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class RestorePasswordModelValidator : AbstractValidator<RestorePasswordViewModel>
    {
        public RestorePasswordModelValidator()
        {
            Include(new TokenVerificationModelValidator());

            RuleFor(a => a.NewPassword).Cascade(CascadeMode.StopOnFirstFailure).NotNull().Matches("^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.{8,})") // old one: "^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[!@#$%^&*])(?=.{8,})")
                .WithMessage("Password must contain at least 1 lowercase alphabetical character, at least 1 uppercase alphabetical character at least 1 numeric character, and must be eight characters or longer"); // old one: at least one special character,
        }
    }
}
