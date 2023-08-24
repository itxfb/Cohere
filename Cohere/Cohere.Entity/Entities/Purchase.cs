using System.Collections.Generic;

using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums.Contribution;

namespace Cohere.Entity.Entities
{
    public class Purchase : BaseEntity
    {
        public string ClientId { get; set; }

        public string ContributorId { get; set; }

        public string ContributionId { get; set; }

        public string ContributionType { get; set; }

        public string SubscriptionId { get; set; }

        public int? SplitNumbers { get; set; }

        public PaymentSplitPeriods? SplitPeriod { get; set; }

        public List<PurchasePayment> Payments { get; set; } = new List<PurchasePayment>();

        public bool IsFirstPaymentHandeled { get; set; }

        public DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase { get; set; }

        public string CouponId { get; set; }
        public bool IsSentToZapier { get; set; } = false;
        public string PaymentType { get; set; }
        public string TaxType { get; set; }
        public bool IsPaidByInvoice { get; set; }
    }
}
