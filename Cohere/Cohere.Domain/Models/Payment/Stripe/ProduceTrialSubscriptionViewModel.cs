namespace Cohere.Domain.Models.Payment.Stripe
{
    public class ProduceTrialSubscriptionViewModel
    {
        public string CustomerId { get; set; }
        
        public string ProductId { get; set; }
        
        public string ContributionId { get; set; }
    }
}