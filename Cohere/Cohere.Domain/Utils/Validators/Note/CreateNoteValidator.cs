using Cohere.Domain.Models.Note;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Note
{
    public class CreateNoteValidator : AbstractValidator<NoteBriefViewModel>
    {
        public CreateNoteValidator()
        {
            RuleFor(x => x.ContributionId)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");

            RuleFor(x => x.ClassId)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");

            //RuleFor(x => x.TextContent)
            //    .NotEmpty()
            //    .WithMessage("{PropertyName} is required");
        }
    }
}
