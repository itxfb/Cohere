namespace Cohere.Domain.Models.Payment.Stripe
{
    public class TrialSubscriptionViewModel
    {
        public string CustomerId { get; set; }

        public string StripeSubscriptionPlanId { get; set; }

        public string ContributionId { get; set; }
    }
}