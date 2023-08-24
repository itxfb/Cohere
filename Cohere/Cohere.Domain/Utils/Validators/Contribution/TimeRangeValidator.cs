using Cohere.Entity.EntitiesAuxiliary.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class TimeRangeValidator : AbstractValidator<TimeRange>
    {
        public TimeRangeValidator()
        {
            RuleFor(c => c.StartTime).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Start time must not be empty");

            RuleFor(c => c.EndTime).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("End time must not be empty");
        }
    }
}