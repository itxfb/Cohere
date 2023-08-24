using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class EventInfo
    {
        public string CalendarEventID { get; set; }
        public string CalendarId { get; set; }
        public string NylasAccountId { get; set; }
        public string AccessToken { get; set; }
        public string ParticipantId { get; set; }
    }
}
