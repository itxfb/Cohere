using Cohere.Domain.Models.User;
using Cohere.Entity.Enums.User;
using FluentValidation;
using System;

namespace Cohere.Domain.Utils.Validators.User
{
    public class UserValidator : AbstractValidator<UserViewModel>
    {
        public UserValidator()
        {
            RuleFor(u => u.AccountId).Cascade(CascadeMode.StopOnFirstFailure).NotEmpty()
                .WithMessage("{PropertyName} not empty")
                .MaximumLength(150).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.Title).Cascade(CascadeMode.StopOnFirstFailure).MinimumLength(3)
                .WithMessage("Minimum length is {MinLength}")
                .MaximumLength(150).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.FirstName).Cascade(CascadeMode.StopOnFirstFailure).NotEmpty()
                .WithMessage("{PropertyName} should not be empty")
                .MinimumLength(1).WithMessage("Minimum length is {MinLength}")
                .MaximumLength(255).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.MiddleName).Cascade(CascadeMode.StopOnFirstFailure).MinimumLength(1)
                .WithMessage("Minimum length is {MinLength}")
                .MaximumLength(255).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.LastName).Cascade(CascadeMode.StopOnFirstFailure).NotEmpty()
                .WithMessage("{PropertyName} should not be empty")
                .MinimumLength(1).WithMessage("Minimum length is {MinLength}")
                .MaximumLength(255).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.NameSuffix).MaximumLength(255).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.AvatarUrl).MaximumLength(500).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.HasAgreedToTerms).Equal(a => true).WithMessage("Terms not accepted");

            RuleFor(u => u.SocialSecurityNumber).MaximumLength(150)
                .WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.StreetAddress).MaximumLength(150)
                .WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.Apt).MaximumLength(50).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.City).MaximumLength(50).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.StateCode).MaximumLength(20).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.Zip).MaximumLength(20).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.CountryCode).MaximumLength(20).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.Bio).MaximumLength(500).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.TimeZoneId).MaximumLength(150).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.CountryId).MaximumLength(150).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(u => u.LanguageCode).MaximumLength(10).WithMessage("{PropertyName} maximum length is {MaxLength}");

            When(u => u.Location != null, () =>
            {
                RuleFor(u => u.Location.Latitude).Cascade(CascadeMode.StopOnFirstFailure)
                    .GreaterThanOrEqualTo(-90).WithMessage("{PropertyName} minimum is {ComparisonValue}")
                    .LessThanOrEqualTo(90).WithMessage("{PropertyName} maximum is {ComparisonValue}");

                RuleFor(u => u.Location.Longitude).Cascade(CascadeMode.StopOnFirstFailure)
                    .GreaterThanOrEqualTo(-180).WithMessage("{PropertyName} minimum is {ComparisonValue}")
                    .LessThanOrEqualTo(180).WithMessage("{PropertyName} maximum is {ComparisonValue}");
            });

            When(u => u.Phone1 != null, () =>
            {
                RuleFor(u => u.Phone1.PhoneNumber).MaximumLength(20)
                    .WithMessage("{PropertyName} maximum length is {MaxLength}");
            });

            When(u => u.Phone1 != null, () =>
            {
                RuleFor(u => u.Phone2.PhoneNumber).MaximumLength(20)
                    .WithMessage("{PropertyName} maximum length is {MaxLength}");
            });

            When(u => u.SocialMediaLinks != null, () =>
            {
                RuleFor(u => u.SocialMediaLinks.Count).LessThanOrEqualTo(Constants.MaxSocialMediaLinksToHave)
                    .WithMessage("You cannot add more links than {ComparisonValue}");
            });
        }
    }
}