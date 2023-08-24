using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasEventCreation
    {
        public string id { get; set; }
        public string account_id { get; set; }
        public string title { get; set; }
        public string calendar_id { get; set; }
        public string status { get; set; }
        public bool busy { get; set; }
        public bool read_only { get; set; }
        public List<Participants> participants { get; set; }
        public string description { get; set; }
        public When when { get; set; }
        public string location { get; set; }
    }
    public class Participants
    {
        public string name { get; set; }
        public string email { get; set; }
        //public string status { get; set; }
    }
    public class When
    {
        public long start_time { get; set; }
        public long end_time { get; set; }
        public string start_timezone { get; set; }
        public string end_timezone { get; set; }
    }
}
