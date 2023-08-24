using Cohere.Domain.Models.Content;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Content
{
    public class AttachmentWithKeyValidator : AbstractValidator<AttachmentWithKeyViewModel>
    {
        public AttachmentWithKeyValidator()
        {
            Include(new GetAttachmentValidator());

            RuleFor(x => x.DocumentKeyWithExtension).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Document key with extension must be not empty")
                .MaximumLength(255).WithMessage("Document key with extension maximum length is {MaxLength}");
        }
    }
}
