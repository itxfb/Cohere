using Cohere.Domain.Models.Payment;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class PurchaseCommunityContributionValidator : AbstractValidator<PurchaseCommunityContributionViewModel>
    {
        public PurchaseCommunityContributionValidator()
        {
            RuleFor(c => c.PaymentOption)
                .NotEmpty().NotNull()
                .IsEnumName(typeof(PaymentOptions))
                .WithMessage("All the payment options must be one of predefined payment types");

            RuleFor(c => c.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} must not be empty.");
        }
    }
}
