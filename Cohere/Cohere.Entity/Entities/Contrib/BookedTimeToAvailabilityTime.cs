using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Entity.Entities.Contrib
{
    public class BookedTimeToAvailabilityTime
    {
        public string ClientName { get; set; }

        public string ContributionName { get; set; }

        public AvailabilityTime AvailabilityTime { get; set; }

        public BookedTime BookedTime { get; set; }
    }
}