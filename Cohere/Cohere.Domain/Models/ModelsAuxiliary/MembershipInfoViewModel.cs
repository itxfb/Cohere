using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Models.ModelsAuxiliary
{
    public class MembershipInfoViewModel
    {
        public Dictionary<string, decimal> Costs { get; set; } = new Dictionary<string, decimal>();

        public MembershipPackageViewModel MembershipPackage { get; set; }

        public Dictionary<string, BillingPlanInfo> ProductBillingPlans { get; set; } =
            new Dictionary<string, BillingPlanInfo>();
    }
}