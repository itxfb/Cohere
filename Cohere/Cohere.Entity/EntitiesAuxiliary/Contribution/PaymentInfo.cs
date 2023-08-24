using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Membership;
using Cohere.Entity.Enums.Contribution;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class PaymentInfo
    {
        public decimal? Cost { get; set; }

        public List<PaymentOptions> PaymentOptions { get; set; }

        public int? SplitNumbers { get; set; }

        public PaymentSplitPeriods? SplitPeriod { get; set; }

        public int? PackageSessionNumbers { get; set; }

        public int? PackageSessionDiscountPercentage { get; set; }

        public MonthlySessionSubscription MonthlySessionSubscriptionInfo { get; set; }

        public MembershipInfo MembershipInfo { get; set; }

        public BillingPlanInfo BillingPlanInfo { get; set; }

        public bool CoachPaysStripeFee { get; set; }

        public decimal? PackageCost { get; set; }
    }
}
