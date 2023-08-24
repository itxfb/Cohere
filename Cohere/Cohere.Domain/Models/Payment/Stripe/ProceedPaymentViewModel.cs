namespace Cohere.Domain.Models.Payment.Stripe
{
    public class ProceedPaymentViewModel
    {
        public string ClientSecret { get; set; }

        public bool IsCancellationAllowed { get; set; }
    }
}
