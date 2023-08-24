using Stripe;

namespace Cohere.Domain.Models.Affiliate
{
    public class CreateMoneyTransferResult
    {
        public Transfer CoachTransfer { get; set; }

        public Transfer AffiliateTransfer { get; set; }
    }
}
