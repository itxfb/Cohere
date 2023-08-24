using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Models.Payment.Stripe
{
    public class ProductSubscriptionViewModel
    {
        public string CustomerId { get; set; }
        public string CouponId { get; set; }
        public string StripeSubscriptionPlanId { get; set; }

        public string DefaultPaymentMethod { get; set; }

        public long? Iterations { get; set; }
        public BillingPlanInfo BillingInfo { set; get; }

        public string ServiceAgreementType { get; set; }

        public string ConnectedStripeAccountId { get; set; }

        public string StandardAccountId { get; set; }

        public string PaymentType { get; set; }

        public PaymentIntentCreateViewModel PaymentIntent_Model { get; set; }
    }
}
