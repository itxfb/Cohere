using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class ContributionOnDashboardViewModel
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string Title { get; set; }
        public string ContributionImage { get; set; }

        public string Type { get; set; }
        public string TimeZoneShortForm { get; set; }
        public ClosestCohealerSession ClosestSession { get; set; }
        public List<Session> Sessions { get; set; } = new List<Session>();
    }
    
}
