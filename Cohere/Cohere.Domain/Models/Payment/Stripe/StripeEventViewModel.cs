namespace Cohere.Domain.Models.Payment.Stripe
{
    public class StripeEventViewModel : BaseDomain
    {
        public string StripeEventId { get; set; }

        public bool IsProcessed { get; set; }
    }
}
