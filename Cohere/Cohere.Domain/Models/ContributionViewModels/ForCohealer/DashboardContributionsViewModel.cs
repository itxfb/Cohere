using System.Collections.Generic;

using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class DashboardContributionsViewModel
    {
        public List<ContributionOnDashboardViewModel> ContributionsForDashboard { get; set; }

        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }

        public IEnumerable<FailedSubscription> CoachDeclinedSubscriptions { get; set; }
    }
    
}
