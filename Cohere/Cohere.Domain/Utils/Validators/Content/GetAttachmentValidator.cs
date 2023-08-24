using Cohere.Domain.Models.Content;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Content
{
    public class GetAttachmentValidator : AbstractValidator<GetAttachmentViewModel>
    {
        public GetAttachmentValidator()
        {
            Include(new AttachmentBaseValidator());

            RuleFor(x => x.DocumentId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Document Id must be not empty")
                .MaximumLength(100).WithMessage("Document Id maximum length is {MaxLength}");
        }
    }
}
