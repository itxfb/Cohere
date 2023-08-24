namespace Cohere.Domain.Models.Payment.Stripe
{
    public class PaymentIntentUpdateViewModel : TransferMoneyViewModel
    {
        public string Id { get; set; }

        public long Amount { get; set; }
    }
}
