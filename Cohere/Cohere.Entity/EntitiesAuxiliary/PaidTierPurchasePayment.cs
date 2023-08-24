using System;
using System.Text.Json.Serialization;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;

namespace Cohere.Entity.EntitiesAuxiliary
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public class PaidTierPurchasePayment
    {
        public string TransactionId { get; set; }

        public PaidTierOptionPeriods PaymentOption { get; set; }

        public DateTime DateTimeCharged { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public decimal TransferAmount { get; set; }

        public decimal PurchaseAmount { get; set; }

        public decimal GrossPurchaseAmount { get; set; }

        public DateTime? PeriodEnds { get; set; }

        public AffiliateRevenueTransfer AffiliateRevenueTransfer { get; set; }
    }
}