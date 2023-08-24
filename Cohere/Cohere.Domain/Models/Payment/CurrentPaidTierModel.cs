using System;
using Cohere.Entity.Entities;

namespace Cohere.Domain.Models.Payment
{
    public class CurrentPaidTierModel
    {
        public PaidTierOption PaidTierOption { get; set; }

        public PaidTierOption NextPaidTierOption { get; set; }

        public DateTime? StartDateTime { get; set; }

        public DateTime? EndDateTime { get; set; }

        public DateTime? CanceledAt { get; set; }

        public DateTime? EndedAtDateTime { get; set; }

        public string Status { get; set; }

        public string CurrentPaymentPeriod { get; set; }
        
        public string CurrentProductPlanId { get; set; }

        public string NextPaymentPeriod { get; set; }

        public int Version { get; set; }
    }

    public class CurrentPaidTierViewModel
    {
        public PaidTierOptionViewModel PaidTierOption { get; set; }

        public PaidTierOptionViewModel NextPaidTierOption { get; set; }

        public DateTime? EndDateTime { get; set; }

        public string Status { get; set; }

        public string CurrentPaymentPeriod { get; set; }

        public string NextPaymentPeriod { get; set; }
    }

    public enum Status
    {
        Active = 1,

        Cancel = 2,

        Upgraded = 3
    }
}