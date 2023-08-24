using Cohere.Entity.Enums.Contribution;

namespace Cohere.Entity.EntitiesAuxiliary.User
{
    public class PaidTierInfo
    {
        public string ProductMonthlyPlanId { get; set; }

        public string ProductAnnuallyPlanId { get; set; }

        public string ProductSixMonthPlanId { set; get; }

        public PaidTierOptionPeriods GetStatus(string productPlanId) =>
                        ProductMonthlyPlanId == productPlanId ? PaidTierOptionPeriods.Monthly : ProductSixMonthPlanId == productPlanId ? PaidTierOptionPeriods.EverySixMonth : PaidTierOptionPeriods.Annually;

    }
}