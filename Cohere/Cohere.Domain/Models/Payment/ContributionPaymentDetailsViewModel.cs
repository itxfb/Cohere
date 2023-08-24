namespace Cohere.Domain.Models.Payment
{
    public class ContributionPaymentDetailsViewModel
    {
        public string Currency { get; set; }

        public decimal Price { get; set; }

        public decimal PlatformFee { get; set; }

        public decimal DueNow { get; set; }

        public decimal DueNowWithCouponDiscountAmount { get; set; }

        public decimal DueLater { get; set; }

        public string Option { get; set; }
    }
}
