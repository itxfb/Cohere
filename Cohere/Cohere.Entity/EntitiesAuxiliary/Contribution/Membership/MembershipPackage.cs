using Cohere.Entity.Enums.Contribution;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution.Membership
{
    public class MembershipPackage
    {
        public PaymentSplitPeriods? Period { get; set; }

        public decimal? Cost { get; set; }

        public int? Duration { get; set; }
    }
}