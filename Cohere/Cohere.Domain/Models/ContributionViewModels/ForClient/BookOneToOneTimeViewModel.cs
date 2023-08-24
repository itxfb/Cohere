namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class BookOneToOneTimeViewModel : BookTimeBaseViewModel
    {
        public string AvailabilityTimeId { get; set; }

        public int Offset { get; set; }
        
        //TODO: use discount codes here
        public string CouponId { get; set; }

        public string AccessCode { get; set; }

        public string PaymentOption { get; set; }

        public bool CreateSingleSession { get; set; }
    }
}
