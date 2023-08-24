using System.Collections.Generic;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class QuestionAnswerPairValidator : AbstractValidator<KeyValuePair<string, string>>
    {
        public QuestionAnswerPairValidator()
        {
            RuleFor(x => x.Key).NotEmpty().WithMessage("Question Id should not be empty");

            RuleFor(x => x.Value).NotEmpty().WithMessage("Answer should not be empty");
        }
    }
}
