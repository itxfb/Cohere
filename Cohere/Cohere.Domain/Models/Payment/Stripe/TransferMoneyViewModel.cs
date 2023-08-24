namespace Cohere.Domain.Models.Payment.Stripe
{
    public class TransferMoneyViewModel
    {
        public long TransferAmount { get; set; }

        public decimal? PurchaseAmount { get; set; }

        public string ConnectedAccountId { get; set; }
    }
}
