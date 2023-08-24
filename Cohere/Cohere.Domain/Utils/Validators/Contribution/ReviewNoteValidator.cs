using Cohere.Domain.Models;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ReviewNoteValidator : AbstractValidator<AdminReviewNoteViewModel>
    {
        public ReviewNoteValidator()
        {
            When(x => x.Status != ContributionStatuses.Approved.ToString(), () =>
                {
                    RuleFor(x => x.Description).Cascade(CascadeMode.StopOnFirstFailure)
                        .NotEmpty().WithMessage("Description allowed to be empty when contribution Approved only")
                        .MaximumLength(500).WithMessage("{PropertyName} maximum length is {MaxLength}");
                });

            RuleFor(x => x.Status).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty()
                .IsEnumName(typeof(ContributionStatuses))
                .WithMessage("{PropertyName} must be one of predefined contribution statuses");
        }
    }
}
