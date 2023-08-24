namespace Cohere.Domain.Models.Payment.Stripe
{
    public class AttachPaymentMethodTokenViewModel
    {
        public string CardToken { get; set; }

        public string ContributionId { set; get; }
    }
}