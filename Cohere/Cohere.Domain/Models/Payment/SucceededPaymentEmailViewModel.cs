using Cohere.Entity.Enums.Contribution;

namespace Cohere.Domain.Models.Payment
{
    public class SucceededPaymentEmailViewModel
    {
        public PaymentOptions PaymentOption { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal CurrentAmount { get; set; }

        public decimal ProcessingFee { get; set; }

        public decimal PurchasePrice { get; set; }
    }
}
