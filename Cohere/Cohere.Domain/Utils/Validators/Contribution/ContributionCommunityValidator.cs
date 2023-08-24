using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ContributionCommunityValidator : AbstractValidator<ContributionCommunityViewModel>
    {
        public ContributionCommunityValidator()
        {
            Include(new ContributionBaseValidator());

            RuleFor(c => c.Type).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty")
                .Equal(nameof(ContributionCommunity)).WithMessage("{PropertyName} must be one of predefined contribution types");

            RuleFor(c => c.Participants).Empty()
                .WithMessage("{PropertyName} is service property and must be empty during creation or update process");

            When(c => c.PaymentInfo != null, () =>
            {
                RuleFor(c => c.PaymentInfo).Must(p => !p.PaymentOptions.Contains(PaymentOptions.SessionsPackage.ToString()))
                    .WithMessage("Session package option is not supported for course contribution");

                When(c => c.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments.ToString()), () =>
                {
                    RuleFor(c => c.PaymentInfo.SplitNumbers)
                        .NotEmpty()
                        .WithMessage("Number of split payments must be not empty")
                        .GreaterThanOrEqualTo(2)
                        .WithMessage("Number of split payments must be greater or equal to {ComparisonValue}")
                        .LessThanOrEqualTo(12)
                        .WithMessage("Number of split payments must be less or equal to {ComparisonValue}");

                    RuleFor(c => c.PaymentInfo.SplitPeriod).IsEnumName(typeof(PaymentSplitPeriods))
                        .WithMessage("Payment period must be one of predefined split periods");
                });
            });
        }
    }
}
