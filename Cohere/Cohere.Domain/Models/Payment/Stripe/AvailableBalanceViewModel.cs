namespace Cohere.Domain.Models.Payment.Stripe
{
    public class AvailableBalanceViewModel
    {
        public string Currency { get; }

        public decimal Amount { get; }
        public decimal AffiliateAmount { get; set; }
        public string StandardAccountCurrency { get; set; }
        public decimal? StandardAccountAmount { get; set; }
        public decimal StandardAccountAffiliateAmount { get; set; }
        public AvailableBalanceViewModel(string currency, decimal amount, decimal affiliateAmount, string standardAccountCurrency, decimal? standardAccountAmount, decimal standardAccountAffiliateAmount)
        {
            Currency = currency;
            Amount = amount;
            AffiliateAmount= affiliateAmount;
            StandardAccountCurrency = standardAccountCurrency;
            StandardAccountAmount = standardAccountAmount;
            StandardAccountAffiliateAmount = standardAccountAffiliateAmount;
        }
    }
}
