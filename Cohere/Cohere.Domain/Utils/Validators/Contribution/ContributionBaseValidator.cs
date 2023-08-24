using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ContributionBaseValidator : AbstractValidator<ContributionBaseViewModel>
    {
        public ContributionBaseValidator()
        {
            Include(new ContributionBaseServiceFieldsValidator());
            Include(new UnfinishedContributionBaseValidator());

            RuleFor(c => c.Purpose).Cascade(CascadeMode.StopOnFirstFailure)
                //.NotEmpty().WithMessage("{PropertyName} must not be empty")
                .MaximumLength(1000).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(c => c.WhoIAm).Cascade(CascadeMode.StopOnFirstFailure)
                //.NotEmpty().WithMessage("Who I Am must not be empty")
                .MaximumLength(1000).WithMessage("Who I Am maximum length is {MaxLength}");

            RuleFor(c => c.WhatWeDo).Cascade(CascadeMode.StopOnFirstFailure)
                //.NotEmpty().WithMessage("What We Do must not be empty")
                .MaximumLength(1000).WithMessage("What we expect maximum length is {MaxLength}");

            RuleFor(c => c.Preparation).Cascade(CascadeMode.StopOnFirstFailure)
                //.NotEmpty().WithMessage("{PropertyName} must not be empty")
                .MaximumLength(1000).WithMessage("{PropertyName} maximum length is {MaxLength}");

            RuleFor(c => c.LanguageCodes).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Language Codes must not be null or empty list");
            When(c => c.LanguageCodes != null, () =>
            {
                RuleForEach(c => c.LanguageCodes).IsEnumName(typeof(ContributionLanguageCodes))
                    .WithMessage("Language Codes must be one of predefined languages available in the application");
            });

            RuleFor(c => c.MinAge).Cascade(CascadeMode.StopOnFirstFailure)
                .MaximumLength(100).WithMessage("Age maximum length is {MaxLength}");

            RuleFor(c => c.Gender).Cascade(CascadeMode.StopOnFirstFailure)
                .IsEnumName(typeof(ContributionGenders))
                .WithMessage("Gender must be one of predefined options available in the application");

            RuleFor(c => c.PaymentInfo).Cascade(CascadeMode.StopOnFirstFailure)
                .NotNull().WithMessage("Payment Info must not be null");
            When(c => c.PaymentInfo != null, () =>
            {
                RuleFor(c => c.PaymentInfo.Cost).GreaterThan(0)
                    .WithMessage($"Contribution cost must be greater than 0");

                RuleFor(c => c.PaymentInfo.PaymentOptions).NotEmpty()
                    .WithMessage("Payment options must not be empty");

                RuleForEach(c => c.PaymentInfo.PaymentOptions)
                    .IsEnumName(typeof(PaymentOptions))
                    .WithMessage("All the payment options must be one of predefined payment types");
            });

            RuleFor(c => c.HasAgreedContributionTerms).NotEmpty().WithMessage("You must agree terms of service");
        }
    }
}
