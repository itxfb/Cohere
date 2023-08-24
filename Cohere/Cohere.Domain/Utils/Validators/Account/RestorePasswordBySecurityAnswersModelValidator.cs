using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class RestorePasswordBySecurityAnswersModelValidator : AbstractValidator<RestoreBySecurityAnswersViewModel>
    {
        public RestorePasswordBySecurityAnswersModelValidator()
        {
            RuleFor(a => a.Email).NotEmpty().WithMessage("{PropertyName} should not be empty");

            RuleForEach(a => a.SecurityAnswers).SetValidator(new QuestionAnswerPairValidator());
        }
    }
}
