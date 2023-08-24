using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
	public class ContributionCommunityViewModel : SessionBasedContributionViewModel
    {
        public ContributionCommunityViewModel(IValidator<ContributionCommunityViewModel> validator)
            : base(validator)
        {
        }

        public SubscriptionStatus SubscriptionStatus { get; set; }
    }
}
