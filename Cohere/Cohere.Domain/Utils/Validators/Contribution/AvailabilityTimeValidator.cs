using Cohere.Entity.EntitiesAuxiliary.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class AvailabilityTimeValidator : AbstractValidator<AvailabilityTime>
    {
        public AvailabilityTimeValidator()
        {
            RuleFor(x => x)
                .Must(x => x.StartTime < x.EndTime)
                .WithMessage(x => "Availability end time should be later than start time");
        }
    }
}
