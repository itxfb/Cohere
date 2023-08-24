using Cohere.Domain.Models.PartnerCoach;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.PartnerCoach
{
    public class InviteParnerCoachValidator : AbstractValidator<InvitePartnerCoachViewModel>
    {
        public InviteParnerCoachValidator()
        {
            RuleFor(x => x.ContributionId)
                .NotEmpty().WithMessage("{PropertyName} is Required")
                .NotNull().WithMessage("{PropertyName} is Required")
                .Length(24).WithMessage("{PropertyName} is invalid");

            RuleForEach(x => x.Emails)
                .NotNull().WithMessage("{PropertyName} is Required")
                .NotEmpty().WithMessage("{PropertyName} is Required")
                .EmailAddress().WithMessage("{PropertyName} must be a valid Email Address");
        }
    }
}
