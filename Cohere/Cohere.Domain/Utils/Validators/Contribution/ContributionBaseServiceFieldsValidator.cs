using Cohere.Domain.Models.ContributionViewModels.Shared;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class ContributionBaseServiceFieldsValidator : AbstractValidator<ContributionBaseViewModel>
    {
        public ContributionBaseServiceFieldsValidator()
        {
            RuleFor(c => c.ServiceProviderName)
                .Empty()
                .WithMessage("Please create a title for the Contribution and select at least one Category that best matches your service");

            RuleFor(c => c.Status)
                .Empty()
                .WithMessage("{PropertyName} is service property and must be empty during creation or update process");

            RuleFor(x => x.TimeZoneId).Empty()
                .WithMessage("Time Zone Id is service property and must be empty during creation or update process");

            RuleFor(c => c.Rating).Empty()
                .WithMessage("{PropertyName} is service property and must be empty during creation or update process");

            RuleFor(c => c.LikesNumber).Empty()
                .WithMessage("{PropertyName} is service property and must be empty during creation or update process");

            RuleFor(c => c.Reviews).Empty()
                .WithMessage("{PropertyName} is service property and must be empty during creation or update process");
        }
    }
}
