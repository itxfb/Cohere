using System.Collections.Generic;

namespace Cohere.Entity.Entities.Contrib
{
    public class EventDiff
    {
        public List<SessionTimeToSession> CreatedEvents { get; set; } = new List<SessionTimeToSession>();

        public List<SessionTimeToSession> UpdatedEvents { get; set; } = new List<SessionTimeToSession>();

        public List<SessionTimeToSession> CanceledEvents { get; set; } = new List<SessionTimeToSession>();

        public List<SessionTimeToSession> NotModifiedEvents { get; set; } = new List<SessionTimeToSession>();
    }
}