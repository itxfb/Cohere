using Cohere.Domain.Utils;
using Cohere.Entity.Enums.Contribution;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Reflection.Metadata;
namespace Cohere.Domain.Models.Payment.Stripe
{
    public class CreateProductWithTaxblePlaneViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StandardAccountId { get; set; } = null;
        public TaxTypes TaxType { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public int IntervalCount { set; get; } = 1;
        public string Interval { get; set; }
        public long Amount { get; set; }
        public int? SplitNumbers { get; set; }  //in case of split payment
        public int? Duration { get; set; } //in case of monthly subscription of OneToOne
        public string Currency { get; set; }

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