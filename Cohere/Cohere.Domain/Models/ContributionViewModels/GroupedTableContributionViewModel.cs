using System.Collections.Generic;

using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables;
using Cohere.Domain.Models.ContributionViewModels.Shared;

namespace Cohere.Domain.Models.ContributionViewModels
{
    public class GroupedTableContributionViewModel
    {
        public string Type { get; set; }
        public string ContributionImage { get; set; }
        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }

        public IEnumerable<ContribTableViewModel> Contributions { get; set; }

        public IEnumerable<ClosestCohealerSession> UpcomingSessions { get; set; }
    }
}
