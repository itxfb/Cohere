using Cohere.Domain.Models.Content;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Content
{
    public class AttachmentBaseValidator : AbstractValidator<AttachmentBaseViewModel>
    {
        public AttachmentBaseValidator()
        {
            RuleFor(x => x.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id must be not empty")
                .MaximumLength(100).WithMessage("Contribution Id maximum length is {MaxLength}");

            RuleFor(x => x.SessionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Session Id must be not empty")
                .MaximumLength(100).WithMessage("Session Id maximum length is {MaxLength}");
        }
    }
}
