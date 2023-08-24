namespace Cohere.Domain.Models.Payment.Plaid
{
    public class FetchStripeTokenViewModel
    {
        public string AccessToken { get; set; }

        public string AccountId { get; set; }
    }
}
