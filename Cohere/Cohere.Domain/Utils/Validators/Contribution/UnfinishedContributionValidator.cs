using Cohere.Domain.Models.ContributionViewModels.Shared;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public class UnfinishedContributionValidator : AbstractValidator<ContributionBaseViewModel>, IUnfinishedContributionValidator
    {
        public UnfinishedContributionValidator()
        {
            Include(new ContributionBaseServiceFieldsValidator());
            Include(new UnfinishedContributionBaseValidator());
        }
    }
}
