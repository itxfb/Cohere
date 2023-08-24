using Cohere.Domain.Models.Account;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Account
{
    public class AccountValidator : AbstractValidator<AccountViewModel>
    {
        public AccountValidator()
        {
            RuleFor(a => a.Email).Cascade(CascadeMode.StopOnFirstFailure).NotNull().WithMessage("{PropertyName} is required")
                .EmailAddress().WithMessage("Email address")
                .MinimumLength(3).WithMessage("{PropertyName} min length is {MinLength}")
                .MaximumLength(100).WithMessage("{PropertyName} max length is {MaxLength}");

            RuleFor(a => a.Password).Cascade(CascadeMode.StopOnFirstFailure).NotNull().Matches("^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.{8,})") // old version: "^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[!@#$%^&*])(?=.{8,})"
                .WithMessage("Password must contain at least 1 lowercase alphabetical character, at least 1 uppercase alphabetical character at least 1 numeric character, and must be eight characters or longer") // old version: at least one special character,
                .MaximumLength(250).WithMessage("{PropertyName} max length is {MaxLength}");

            When(a => a.SecurityAnswers != null, () =>
            {
                RuleFor(a => a.SecurityAnswers.Count).Cascade(CascadeMode.StopOnFirstFailure)
                    .GreaterThanOrEqualTo(Constants.MinSecurityQuestionsNumber).WithMessage("Min 3 questions");
            });

            RuleFor(a => a.IsPushNotificationsEnabled).NotNull().WithMessage("Info if push notifications enabled is required");

            RuleFor(a => a.IsEmailNotificationsEnabled).NotNull().WithMessage("Info if email notifications enabled is required");
        }
    }
}
