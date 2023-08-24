using System;
using System.Collections.Generic;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class Session
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Title { get; set; }

        public int? MinParticipantsNumber { get; set; }

        public int? MaxParticipantsNumber { get; set; }

        public List<SessionTime> SessionTimes { get; set; } = new List<SessionTime>();

        public bool IsCompleted { get; set; }

        public DateTime? CompletedDateTime { get; set; }

        public List<Document> Attachments { get; set; } = new List<Document>();

        public bool IsPrerecorded { get; set; }

        public DateTime? DateAvailable { get; set; }

        public Document PrerecordedSession { get; set; }

        public string MoreInfo { get; set; }
        public bool IsHappeningToday { get; set; }
    }
}
