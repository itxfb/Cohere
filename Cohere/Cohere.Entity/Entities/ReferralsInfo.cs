using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class ReferralsInfo : BaseEntity
    {
        public string ReferredUserId { get; set; }

        public string ReferralUserId { get; set; }

        public decimal ReferralAmount { get; set; }

        public Boolean IsPaidOut { set; get; }

        public Boolean IsTransferred { set; get; }

        public DateTime TransferTime { set; get; }

        public DateTime? PaidOutTime { set; get; }

    }
}