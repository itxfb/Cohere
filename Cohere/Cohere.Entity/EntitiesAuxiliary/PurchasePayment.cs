using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;

namespace Cohere.Entity.EntitiesAuxiliary
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public class PurchasePayment
    {
        public string TransactionId { get; set; }

        public bool IsTrial { get; set; }

        public string InvoiceId { get; set; }

        public PaymentOptions PaymentOption { get; set; }

        public DateTime DateTimeCharged { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public decimal TransferAmount { get; set; }

        public string TransferCurrency { get; set; }

        public decimal PurchaseAmount { get; set; }

        public string PurchaseCurrency { get; set; }

        public decimal ExchangeRate { get; set; }
        
        public string Currency { get; set; }

        public decimal ProcessingFee { get; set; }

        public decimal CohereFee { get; set; }

        public decimal CoachFee { get; set; }

        public decimal ClientFee { get; set; }

        public decimal GrossPurchaseAmount { get; set; }

        public decimal TotalCost { get; set; }

        public string CouponId { get; set; }

        public bool IsInEscrow { get; set; }

        public DateTime? PeriodEnds { get; set; }

        public List<string> BookedClassesIds { get; set; } = new List<string>();

        public AffiliateRevenueTransfer AffiliateRevenueTransfer { get; set; }
        public DestinationBalanceTransaction DestinationBalanceTransaction { get; set; }

        public bool HasBookedClassId(string classId) => BookedClassesIds.Contains(classId);
       
        public bool IsAccessRevokedByCoach { get; set; }

        public bool IsAccessRevoked { get; set; }
    }
}
