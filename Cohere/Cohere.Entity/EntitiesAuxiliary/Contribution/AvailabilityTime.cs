using System.Collections.Generic;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class AvailabilityTime : TimeRange
    {
        public string Id { get; set; }

        public int Offset { get; set; }

        public List<BookedTime> BookedTimes { get; set; } = new List<BookedTime>();
    }
}
