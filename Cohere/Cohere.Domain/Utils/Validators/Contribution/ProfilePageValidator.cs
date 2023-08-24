using Cohere.Domain.Models.ContributionViewModels.Shared;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ProfilePageValidator : AbstractValidator<ProfilePageViewModel>
    {
        public ProfilePageValidator()
        {
            RuleFor(c => c.UserId).Cascade(CascadeMode.StopOnFirstFailure)
                            .NotEmpty().WithMessage("{PropertyName} must not be empty");
            // RuleFor(u => u.GroupContributions.Count).LessThanOrEqualTo(10).WithMessage("{PropertyName} maximum count is 10");
            // RuleFor(u => u.OneToOneContributions.Count).LessThanOrEqualTo(10).WithMessage("{PropertyName} maximum count is 10");
            //RuleFor(u => u.CommunityContributions.Count).LessThanOrEqualTo(10).WithMessage("{PropertyName} maximum count is 10");
            //RuleFor(u => u.MembershipContributions.Count).LessThanOrEqualTo(10).WithMessage("{PropertyName} maximum count is 10");
        }
    }
}
