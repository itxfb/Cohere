using System.Collections.Generic;

using Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables;
using Cohere.Domain.Models.ContributionViewModels.Shared;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class AllUpcomingContributionsForCohealer
    {
        public string AuthorAvatarUrl { get; set; }

        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }

        public List<ContribTableViewModel> ContributionsForTable { get; set; } = new List<ContribTableViewModel>();
        public int TotalCount { get; set; }
    }
}
