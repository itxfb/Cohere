using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Cohere.Domain.Models.Payment.Stripe
{
    public class AttachPaymentMethodViewModel
    {
        public string PaymentMethodId { get; set; }

        public string ContributionId { set; get; }
    }
}
