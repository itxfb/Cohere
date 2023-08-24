namespace Cohere.Domain.Models.Payment
{
    public class UpgradePaidTierPlanModel
    {
        public string PaidTierId { get; set; }

        public string PaymentOption { get; set; }
    }
}