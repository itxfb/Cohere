using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyPastClassesViewModel
    {
        public IEnumerable<JourneyClassViewModel> ThisWeek { get; set; }

        public IEnumerable<JourneyClassViewModel> ThisMonth { get; set; }

        public IEnumerable<JourneyClassViewModel> LastMonth { get; set; }

        public IEnumerable<JourneyClassViewModel> ThisYear { get; set; }

        public IEnumerable<JourneyClassViewModel> PriorYears { get; set; }
    }
}
