using Cohere.Domain.Utils;
using Cohere.Entity.Enums.Contribution;
using System.Collections.Generic;

namespace Cohere.Domain.Models.Payment.Stripe
{
    public class CreateProductPlanViewModel
    {
        public string Name { get; set; }

        public string ProductId { get; set; }

        public long Amount { get; set; }

        public string Interval { get; set; }

        public int? SplitNumbers { get; set; } //in case of split payment
        public int? Duration { get; set; } //in case of monthly subscription of OneToOne

        public Dictionary<string, string> Metadata { get; set; }

        public int IntervalCount { set; get; } = 1;

        public Dictionary<string, string> GetMetadata()
        {
            if (Duration.HasValue)
            {
                return new Dictionary<string, string>()
                    {
                        {Constants.Stripe.MetadataKeys.PaymentOption, PaymentOptions.MonthlySessionSubscription.ToString()}
                    };
            }
            if (SplitNumbers.HasValue)
            {
                return new Dictionary<string, string>()
                    {
                        {Constants.Stripe.MetadataKeys.PaymentOption, PaymentOptions.SplitPayments.ToString()}
                    };
            }
            return null;
        }
    }
}
