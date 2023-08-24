namespace Cohere.Domain.Models.Payment
{
    public class PackagePaymentDetailViewModel
    {
        public int SessionNumbers { get; set; }

        public int BookedSessionNumbers { get; set; }

        public int FreeSessionNumbers => SessionNumbers - BookedSessionNumbers;
    }
}
