using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyClassesInfosAll
    {
        public List<JourneyClassInfo> Upcoming { get; set; } = new List<JourneyClassInfo>();

        public List<JourneyClassInfo> Past { get; set; } = new List<JourneyClassInfo>();

        public List<JourneyClassInfo> NotBooked { get; set; } = new List<JourneyClassInfo>();

        public List<JourneyClassInfo> OtherUncompleted { get; set; } = new List<JourneyClassInfo>();

        public List<JourneyClassInfo> OtherCompleted { get; set; } = new List<JourneyClassInfo>();
    }
    public class JourneyClassesInfosAllUpdated
    {
        public List<JourneyClassInfo> Upcoming { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> InCompleted { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> Bookable { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> Modules { get; set; } = new List<JourneyClassInfo>();
        public List<JourneyClassInfo> Past { get; set; } = new List<JourneyClassInfo>();
    }
}
