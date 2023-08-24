using CsvHelper.Configuration.Attributes;

namespace Cohere.Domain.Models.Payment
{
    public class ContributionSaleViewModel
    {
        [Name("PYMT DATE")]
        public string PaymentDate { get; set; }

        [Name("FIRST NAME")]
        public string FirstName { get; set; }

        [Name("LAST NAME")]
        public string LastName { get; set; }

        [Name("CONTACT")]
        public string Contact { get; set; }

        [Name("CONTRIBUTION")]
        public string ContributionName { get; set; }

        [Name("CURRENCY")]
        public string Currency { get; set; }

        [Name("TOTAL COST")]
        public decimal TotalCost { get; set; }

        [Name("PYMT AMT")]
        public decimal PaymentAmount { get; set; }

        [Name("COHERE FEE")]
        public decimal Fee { get; set; }

        [Name("COACH PYMT PROCESSING FEE")]
        public decimal CoachFee { get; set; }

        [Name("CLIENT PYMT PROCESSING FEE")]
        public decimal ClientFee { get; set; }

        //[Name("PYMT PROCESSING FEE")]
        //public decimal ProcessingFee { get; set; }

        [Name("COUPON NAME")]
        public string CouponName { get; set; }

        [Name("$ NET REVENUE EARNED")]
        public decimal ReveueEarned { get; set; }

        [Name("REVENUE FOR PAYOUT")]
        public string ReveuePayout { get; set; }

        [Name("EXCHAGE RATE")]
        public string ExhangeRate { get; set; }

        [Name("IN ESCROW")]
        public decimal InEscrow { get; set; }

        [Name("PENDING PAYMENTS")]
        public string PendingPayments { get; set; }

        [Name("SOURCE")]
        public string Source { get; set; }

        [Name("PAYMENT PROCESSEOR")]
        public string PaymentType { get; set; }
        [Name("TAX TYPE")]
        public string TaxType { get; set; }

        [Name("REVENUE COLLECTION")]
        public string RevenueCollection { get; set; }

        [Name("TAX HISTORY LINK")]
        public string TaxHistoryLink { get; set; }
        //[Name("GrossSales")]
        //public decimal GrossSales { get; internal set; }

        [Name("Has Access?")]
        public string HasAccess { get; set; }
    }
}
