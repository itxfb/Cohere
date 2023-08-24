namespace Cohere.Domain.Models.Payment.Stripe
{
    public class GetPlanSubscriptionViewModel
    {
        public string CustomerId { get; set; }

        public string SubscriptionPlanId { get; set; }
    }
}
