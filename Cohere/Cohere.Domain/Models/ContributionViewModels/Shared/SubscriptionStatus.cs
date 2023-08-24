using System;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class SubscriptionStatus
    {
        public string Status { get; set; }

        /// <summary>
        /// End Period (UTC)
        /// </summary>
        public DateTime EndPeriod { get; set; }

        public string PaymentOption { get; set; }

        public string NextPaymentOption { get; set; }
    }
}