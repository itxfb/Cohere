using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class ClosestCohealerSessionInfo
    {
        public string Title { get; set; }

        public string Name { get; set; }

        public List<string> ParticipantsIds { get; set; }

        public int EnrolledTotal { get; set; }

        public int? EnrolledMax { get; set; }

        public DateTime StartTime { get; set; }

        public string ClassId { get; set; }

        public string ClassGroupId { get; set; }

        public string ChatSid { get; set; }

        public bool IsPrerecorded { get; set; }
        public bool IsCompleted { get; set; }

        public string ZoomStartMeeting { get; set; }
        public List<SessionTime> SessionTimes { get; set; } = new List<SessionTime>();
    }
}
