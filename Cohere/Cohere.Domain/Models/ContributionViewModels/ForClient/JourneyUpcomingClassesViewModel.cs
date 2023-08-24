using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyUpcomingClassesViewModel
    {
        public IEnumerable<JourneyClassViewModel> ThisWeek { get; set; }

        public IEnumerable<JourneyClassViewModel> ThisMonth { get; set; }

        public IEnumerable<JourneyClassViewModel> NextMonth { get; set; }

        public IEnumerable<JourneyClassViewModel> ThisYear { get; set; }

        public IEnumerable<JourneyClassViewModel> AfterThisYear { get; set; }

        public IEnumerable<JourneyClassViewModel> OtherIncompleted { get; set; }

        public IEnumerable<JourneyClassViewModel> NotBooked { get; set; }
    }
}
