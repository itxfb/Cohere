using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ContributionCourseValidator : AbstractValidator<ContributionCourseViewModel>
    {
        public ContributionCourseValidator()
        {
            Include(new ContributionBaseValidator());

            RuleFor(c => c.Type).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("{PropertyName} must not be empty")
                .Equal(nameof(ContributionCourse)).WithMessage("{PropertyName} must be one of predefined contribution types");

            //RuleFor(c => c.Sessions).Cascade(CascadeMode.StopOnFirstFailure).NotEmpty()
            //    .WithMessage("{PropertyName} count must be not empty");

            When(c => c.Sessions.Count > 0, () =>
            {
                RuleForEach(x => x.Sessions).Cascade(CascadeMode.StopOnFirstFailure)
                    .NotEmpty()
                    .SetValidator(opt => new SessionValidator());
            });

            RuleFor(c => c.Enrollment).Cascade(CascadeMode.StopOnFirstFailure)
                .NotNull().WithMessage("{PropertyName} must not be null");
            When(c => c.Enrollment != null, () =>
            {
                RuleFor(c => c.Enrollment.ToDate).GreaterThan(c => c.Enrollment.FromDate)
                    .WithMessage("Enrollment To Date must be later than Enrollment From Date");
            });

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
