using Cohere.Entity.EntitiesAuxiliary.User;

namespace Cohere.Entity.Entities
{
    public class PaidTierOption : BaseEntity
    {
        public string DisplayName { get; set; }
        public string DisplayNameExtraText { get; set; }

        public long PricePerMonth { get; set; }

        public long PricePerYear { get; set; }

        public long PricePerSixMonth { get; set; }

        public long Fee { get; set; }

        public decimal NormalizedFee => Fee / 100m;

        public int AffiliatePartInPercents { get; set; }

        public string[] Advantages { get; set; }

        public bool CanBeUpgraded { get; set; }

        public bool IsActive { get; set; }

        public string Expires { get; set; }

        public bool Default { get; set; }

        public int Order { get; set; }

        public PaidTierInfo PaidTierInfo { get; set; }

        public int Version { get; set; }
    }
}