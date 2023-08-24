using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;
using System;
using System.Linq;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ContributionOneToOneValidator : AbstractValidator<ContributionOneToOneViewModel>
    {
        public ContributionOneToOneValidator()
        {
            Include(new ContributionBaseValidator());

            RuleFor(c => c.Type).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty")
                .Equal(nameof(ContributionOneToOne)).WithMessage("{PropertyName} must be one of predefined contribution types");

            RuleFor(c => c.OneToOneSessionDataUi).Cascade(CascadeMode.StopOnFirstFailure)
                .NotNull()
                .WithMessage("Contribution sessions should be scheduled");

            RuleFor(c => c.AvailabilityTimes).Cascade(CascadeMode.StopOnFirstFailure).NotNull()
                .WithMessage("Availability times should be defined");

            When(c => c.AvailabilityTimes.Count > 0, () =>
            {
                RuleForEach(c => c.AvailabilityTimes)
                    .SetValidator(opt => new AvailabilityTimeValidator());
            });

            RuleFor(c => c.Durations)
                .NotEmpty()
                .WithMessage("{PropertyName} list must not be empty.");
            When(c => c.Durations.Count > 0, () =>
            {
                RuleForEach(c => c.Durations).Must(d => Enum.GetValues(typeof(OneToOneDurations)).Cast<int>().Contains(d))
                    .WithMessage("All the durations must be one of predefined durations list");
            });

            When(c => c.PaymentInfo != null, () =>
            {
                RuleFor(c => c.PaymentInfo).Must(p => !p.PaymentOptions.Contains(PaymentOptions.SplitPayments.ToString()))
                    .WithMessage("Split payments option is not supported for one-to-one contribution");

                When(c => c.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage.ToString()), () =>
                {
                    RuleFor(c => c.PaymentInfo.PackageSessionNumbers)
                        .NotNull()
                        .WithMessage("Number of package sessions must be not empty")
                        .GreaterThanOrEqualTo(2)
                        .WithMessage("Number of package sessions must be greater or equal to {ComparisonValue}");

                    When(c => c.PaymentInfo.PackageSessionDiscountPercentage.HasValue, () =>
                    {
                        RuleFor(c => c.PaymentInfo.PackageSessionDiscountPercentage).Cascade(CascadeMode.StopOnFirstFailure)
                            .GreaterThan(0)
                            .WithMessage("Package sessions discount percentage must be greater than {ComparisonValue}")
                            .LessThanOrEqualTo(100)
                            .WithMessage("Package sessions discount percentage must be less or equal to {ComparisonValue}");
                    });
                });

                When(c => c.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription.ToString()), () =>
                {
                    RuleFor(c => c.PaymentInfo.MonthlySessionSubscriptionInfo)
                        .NotNull()
                        .WithMessage("Monthly session subscription must be not empty");

                    When(c => c.PaymentInfo.MonthlySessionSubscriptionInfo != null, () =>
                    {
                        RuleFor(c => c.PaymentInfo.MonthlySessionSubscriptionInfo.SessionCount)
                            .NotNull()
                            .WithMessage("Subscription session count should be defined");

                        RuleFor(c => c.PaymentInfo.MonthlySessionSubscriptionInfo.Duration)
                            .NotNull()
                            .WithMessage("Subscription duration should be defined");

                        RuleFor(c => c.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice)
                            .NotNull()
                            .WithMessage("Subscription Monthly Costs should be defined");
                    });
                });

                When(c => c.PaymentInfo.PaymentOptions.Contains(PaymentOptions.PerSession.ToString()), () =>
                {
                    RuleFor(c => c.PaymentInfo.Cost)
                    .NotNull().NotEmpty()
                    .WithMessage("Per session cost must be not empty");                  
                });

                When(c => c.PaymentInfo.PaymentOptions.Contains(PaymentOptions.PerSession.ToString()), () =>
                {
                    RuleFor(c => c.PaymentInfo.Cost)
                    .NotNull().NotEmpty()
                    .WithMessage("Per session cost must be not empty");                  
                });
            });
        }
    }
}
