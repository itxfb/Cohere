using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class TokenVerificationModelValidator : AbstractValidator<TokenVerificationViewModel>
    {
        public TokenVerificationModelValidator()
        {
            RuleFor(a => a.Email).NotEmpty().WithMessage("{PropertyName} should not be empty");

            RuleFor(a => a.Token).NotEmpty().WithMessage("{PropertyName} should not be empty");
        }
    }
}
