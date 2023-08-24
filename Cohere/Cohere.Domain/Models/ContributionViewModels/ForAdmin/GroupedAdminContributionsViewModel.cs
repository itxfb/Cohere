using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForAdmin
{
    public class GroupedAdminContributionsViewModel
    {
        public IEnumerable<ContributionAdminBriefViewModel> Review { get; set; }

        public IEnumerable<ContributionAdminBriefViewModel> Updated { get; set; }

        public IEnumerable<ContributionAdminBriefViewModel> Approved { get; set; }

        public IEnumerable<ContributionAdminBriefViewModel> Rejected { get; set; }
    }
}
