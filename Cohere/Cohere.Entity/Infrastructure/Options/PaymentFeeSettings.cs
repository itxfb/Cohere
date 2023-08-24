namespace Cohere.Entity.Infrastructure.Options
{
    public class PaymentFeeSettings
    {
        public decimal StripeFixedFee { get; set; }

        public decimal StripePercentageFee { get; set; }

        public decimal PlatformPercentageFee { get; set; }

        public decimal StripeInternationalCardPercentageFee { get; set; }
    }
}
