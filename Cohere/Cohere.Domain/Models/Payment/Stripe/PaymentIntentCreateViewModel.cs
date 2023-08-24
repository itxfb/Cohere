using System.Collections.Generic;

namespace Cohere.Domain.Models.Payment.Stripe
{
    public class PaymentIntentCreateViewModel : TransferMoneyViewModel
    {
        public string CustomerId { get; set; }

        public long Amount { get; set; }

        public string ReceiptEmail { get; set; }
        public string CouponID { get; set; }
        public decimal? CouponPercent { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public string ServiceAgreementType { get; set; }
        public decimal? Fee { get; set; }
        public decimal? Fixed { get; set; }
        public decimal? International { get; set; }
        public decimal? TotalChargedCost { get; set; }
        public bool CoachPaysFee { get; set; }
    }
}
