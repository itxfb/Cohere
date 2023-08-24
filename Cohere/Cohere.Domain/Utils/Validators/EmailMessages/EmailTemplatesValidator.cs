using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Utils.Validators.EmailMessages
{
    public class EmailTemplatesValidator : AbstractValidator<EmailTemplatesViewModel>
    {
        public EmailTemplatesValidator()
        {
            RuleFor(u => u.ContributionId).Cascade(CascadeMode.StopOnFirstFailure).NotNull().WithMessage("Contribution Id cannot be null");

            RuleFor(u => u.CustomTemplates.Select(a => a.EmailType).Count()).NotEqual(0).WithMessage("Custom Template cannot be null");

        }
    }
}
