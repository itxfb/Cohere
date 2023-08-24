using System;

namespace Cohere.Entity.Entities
{
    public class DeclinedSubscriptionPurchase
    {
        public DateTime LastPaymentFailedDate { get; set; }

        public string AmountPaid { get; set; }

        public string AmountDue { get; set; }

        public string AmountRemaining { get; set; }
    }
}