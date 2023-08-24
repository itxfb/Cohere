using System.Collections.Generic;

using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Membership;

namespace Cohere.Domain.Models.ModelsAuxiliary
{
    public class PaymentInfoViewModel
    {
        public decimal? Cost { get; set; }

        public List<string> PaymentOptions { get; set; }

        public int? SplitNumbers { get; set; }

        public string SplitPeriod { get; set; }

        public int? PackageSessionNumbers { get; set; }

        public int? PackageSessionDiscountPercentage { get; set; }

        public MonthlySessionSubscription MonthlySessionSubscriptionInfo { get; set; }

        public MembershipInfoViewModel MembershipInfo { get; set; }

        public BillingPlanInfo BillingPlanInfo { get; set; }

        public bool CoachPaysStripeFee { get; set; } = true;

        public decimal? PackageCost { get; set; }
    }
}
