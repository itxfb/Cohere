using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class LoginValidator : AbstractValidator<LoginViewModel>
    {
        public LoginValidator()
        {
            RuleFor(u => u.Email).NotEmpty().WithMessage("{PropertyName} should not be empty");

            RuleFor(u => u.Password).NotEmpty().WithMessage("{PropertyName} should not be empty");
        }
    }
}
