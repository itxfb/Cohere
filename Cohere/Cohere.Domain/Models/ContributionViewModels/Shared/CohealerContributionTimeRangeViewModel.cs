using System;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class CohealerContributionTimeRangeViewModel
    {
        public string ContributionId { get; set; }

        public string ContributionType { get; set; }

        public string SessionName { get; set; }

        public DateTime SessionStartTime { get; set; }

        public DateTime SessionEndTime { get; set; }

        public string EventId { get; set; }
    }
}