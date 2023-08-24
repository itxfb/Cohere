namespace Cohere.Domain.Models.Payment.Stripe
{
    public class UpdatePaymentMethodViewModel
    {
        public string Id { get; set; }

        public string PaymentMethodId { get; set; }
    }
}
