using Cohere.Domain.Models.Video;
using FluentValidation;
using System;

namespace Cohere.Domain.Utils.Validators.Video
{
    public class GetVideoTokenModelValidator : AbstractValidator<GetVideoTokenViewModel>
    {
        public GetVideoTokenModelValidator()
        {
            RuleFor(x => x.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id to connect should not be empty")
                .MaximumLength(50).WithMessage("Contribution Id maximum length is {MaxLength}");

            RuleFor(x => x.ClassId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Class Id to connect should not be empty")
                .MaximumLength(50).WithMessage("Class Id maximum length is {MaxLength}");

            RuleFor(x => x.IdentityName).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Identity name to use in video room to connect should not be empty")
                .MaximumLength(150).WithMessage("Identity name maximum length is {MaxLength}")
                .Must(x =>
                {
                    foreach (var badWord in Constants.BadWordsArray)
                    {
                        if (x.Contains(badWord, StringComparison.CurrentCultureIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .WithMessage("Your identity name contains bad word. Please use a valid identity name");
        }
    }
}
