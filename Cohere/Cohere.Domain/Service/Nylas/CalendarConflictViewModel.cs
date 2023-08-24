using System;

namespace Cohere.Domain.Service.Nylas
{
    public class CalendarConflictViewModel
    {
        public string CohealerContributionId { get; set; }

        public string CohealerContributionTitle { get; set; }

        public DateTime CohealerSessionStartTime { get; set; }

        public DateTime CohealerSessionEndTime { get; set; }

        public DateTime CohealerExternalCalendarEventStartTime { get; set; }

        public DateTime CohealerExternalCalendarEventEndTime { get; set; }
    }
}