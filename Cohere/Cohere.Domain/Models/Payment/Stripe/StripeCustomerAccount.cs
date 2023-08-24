using System;
namespace Cohere.Domain.Models.Payment.Stripe
{
    public class StripeCustomerAccount
    {
        public string CustomerId { set; get; }
        public string Currency { set; get; }
    }
}
