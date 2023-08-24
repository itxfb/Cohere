using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Nylas
{
    public class CalendarResponse
    {
        public string account_id { get; set; }

        public string description { get; set; }

        public string id { get; set; }

        public bool is_primary { get; set; }

        public string location { get; set; }
        public string name { get; set; }

       // public string object { get; set; }
        public bool read_only { get; set; }
    }

}
