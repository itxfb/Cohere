namespace Cohere.Domain.Models.Payment.Stripe
{
    public class BankAccountAttachedViewModel
    {
        public string Id { get; set; }

        public string BankName { get; set; }

        public string Last4 { get; set; }

        public bool IsDefaultForCurrency { get; set; }

        public bool IsStandard { set; get; }
    }
}
