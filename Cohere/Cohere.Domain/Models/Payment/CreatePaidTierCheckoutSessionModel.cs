namespace Cohere.Domain.Models.Payment
{
    public class CreatePaidTierCheckoutSessionModel
    {
        public string PaidTierId { get; set; }

        public string PaidTierPeriod { get; set; }

        public string ClientAccountId { get; set; }
    }
}