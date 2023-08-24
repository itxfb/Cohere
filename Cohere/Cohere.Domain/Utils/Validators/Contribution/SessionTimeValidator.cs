using Cohere.Entity.EntitiesAuxiliary.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class SessionTimeValidator : AbstractValidator<SessionTime>
    {
        public SessionTimeValidator()
        {
            RuleFor(x => x)
                .Must(x => x.StartTime < x.EndTime)
                .WithMessage(x => "Session end time should be later than start time.");
        }
    }
}
