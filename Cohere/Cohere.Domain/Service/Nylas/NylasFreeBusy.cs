using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasFreeBusy
    {
        public string @object { get; set; }

        public string email { get; set; }

        public IEnumerable<NylasTimeSlot> time_slots { get; set; }

        public IEnumerable<TimeRange> TimeRanges => time_slots?.Select(x => x.ToTimeRange()) ?? new List<TimeRange>();
    }
}