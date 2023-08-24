using Cohere.Entity.EntitiesAuxiliary.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class SessionValidator : AbstractValidator<Session>
    {
        public SessionValidator()
        {
            RuleFor(x => x.Name).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Session {PropertyName} must be not empty.");

            When(
                x => x.MaxParticipantsNumber.HasValue,
                () => RuleFor(x => x.MaxParticipantsNumber).Must(num => num >= 1)
                    .WithMessage("Max participants number must be greater or equal to 1"));

            When(
                x => x.MinParticipantsNumber.HasValue,
                () => RuleFor(x => x.MinParticipantsNumber).Must(num => num >= 1)
                    .WithMessage("Min participants number must be greater or equal to 1"));

            When(
                x => x.MaxParticipantsNumber.HasValue && x.MinParticipantsNumber.HasValue,
                () => RuleFor(x => x).Must(x => x.MaxParticipantsNumber >= x.MinParticipantsNumber)
                    .WithMessage(x => "Max participants number must be greater or equal Min participants number."));

            RuleFor(x => x.SessionTimes).NotEmpty()
                .WithMessage("Session times list must be not empty.");
            When(x => !x.IsPrerecorded && x.SessionTimes != null, () =>
            {
                RuleForEach(x => x.SessionTimes)
                    .Cascade(CascadeMode.StopOnFirstFailure)
                    .NotEmpty().WithMessage("Some session time is null or empty")
                    .SetValidator(opt => new SessionTimeValidator());
            });

            When(x => x.Attachments != null, () =>
            {
                When(x => x.Attachments.Count > 0, () =>
                {
                    RuleForEach(x => x.Attachments).SetValidator(opt => new DocumentValidator());
                });
            });
        }
    }
}
