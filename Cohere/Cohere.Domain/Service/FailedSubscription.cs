using Cohere.Entity.Entities;

namespace Cohere.Domain.Service
{
    public class FailedSubscription
    {
        public string ContributionName { get; set; }

        public string ClientName { get; set; }

        public DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase { get; set; }
    }
}