using System.Collections.Generic;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyClassesAllViewModel
    {
        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }

        public JourneyUpcomingClassesViewModel Upcoming { get; set; }

        public int UpcomingTotalCount { get; set; }

        public JourneyPastClassesViewModel Past { get; set; }

        public int PastTotalCount { get; set; }

        public IEnumerable<FailedSubscription> ClientDeclinedSubscriptions { get; set; }
    }
    public class JourneyClassesAllViewModelUpdated
    {
        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }
        public int UpcomingTotalCount { get; set; }
        public List<JourneyClassInfo> Upcoming { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> InCompletetd { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> Bookable { get; set; } = new List<JourneyClassInfo>();
        public int BookableTotalCount { get; set; }
        public List<JourneyClassInfo> Modules { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> Past { get; set; } = new List<JourneyClassInfo>();
        public int PastTotalCount { get; set; }
        public IEnumerable<FailedSubscription> ClientDeclinedSubscriptions { get; set; }
    }
}
