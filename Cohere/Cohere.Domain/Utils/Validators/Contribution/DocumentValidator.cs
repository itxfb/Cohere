using Cohere.Entity.EntitiesAuxiliary.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class DocumentValidator : AbstractValidator<Document>
    {
        public DocumentValidator()
        {
            RuleFor(x => x.Id).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Document Id must be not empty")
                .MaximumLength(100).WithMessage("Document Id maximum length is {MaxLength}");

            RuleFor(x => x.ContentType).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Content Type must be not empty")
                .MaximumLength(250).WithMessage("Content type maximum length is {MaxLength}");

            RuleFor(x => x.DocumentOriginalNameWithExtension).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Document original name must be not empty")
                .MaximumLength(500).WithMessage("Document original name maximum length is {MaxLength}");

            RuleFor(x => x.DocumentKeyWithExtension).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Document key with extension must be not empty")
                .MaximumLength(255).WithMessage("Document key with extension maximum length is {MaxLength}");
        }
    }
}
