using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities.Contrib
{
    public class SignoffInfo : BaseEntity
    {
        public string ContributionId { get; set; }
        public string AccountId { get; set; }
        public string IPAddress { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
