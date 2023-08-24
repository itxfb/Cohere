namespace Cohere.Domain.Models.Payment.Stripe
{
    public class GetPaidViewModel
    {
        public decimal Amount { get; set; }
        public bool IsStandardAccount { get; set; }
    }
}
