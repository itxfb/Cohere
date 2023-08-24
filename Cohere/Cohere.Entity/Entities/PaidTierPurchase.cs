using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary;

namespace Cohere.Entity.Entities
{
    public class PaidTierPurchase : BaseEntity
    {
        public string ClientId { get; set; }

        public string SubscriptionId { get; set; }

        public List<PaidTierPurchasePayment> Payments { get; set; }

        public bool IsFirstPaymentHandled { get; set; }

        public DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase { get; set; }
    }
}