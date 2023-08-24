namespace Cohere.Domain.Models.Payment
{
    public class PurchaseCourseContributionViewModel : PurchaseContributionViewModel
    {
        public string PaymentOptions { get; set; }

        public string PaymentMethodId { get; set; }
    }
}
