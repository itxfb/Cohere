using FluentValidation;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ContributionMembershipViewModel : SessionBasedContributionViewModel
    {
        public ContributionMembershipViewModel(IValidator<ContributionMembershipViewModel> validator)
            : base(validator)
        {
        }

        public SubscriptionStatus SubscriptionStatus { get; set; }
    }
}
