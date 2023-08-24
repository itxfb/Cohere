using Cohere.Domain.Models.ContributionViewModels.Shared;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Contribution
{
    public interface IUnfinishedContributionValidator : IValidator<ContributionBaseViewModel>
    {
    }
}