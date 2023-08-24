using Cohere.Entity.Entities.Contrib;

namespace Cohere.Domain.Models.Payment
{
    public class PaidTierOptionViewModel : BaseDomain
    {
        public string DisplayName { get; set; }
        public string DisplayNameExtraText { get; set; }

        public long PricePerMonth { get; set; }

        public long PricePerSixMonth { set; get; }

        public long PricePerMonthInCents => PricePerMonth * 100;

        public long PricePerYear { get; set; }

        public long PricePerYearInCents => PricePerYear * 100;

        public long PricePerSixMonthInCents => PricePerSixMonth * 100;

        public int AffiliatePartInPercents { get; set; }

        public long? Fee { get; set; }

        public string[] Advantages { get; set; }

        public bool CanBeUpgraded { get; set; }

        public bool IsActive { get; set; }

        public string Expires { get; set; }

        public bool Default { get; set; }

        public int Order { get; set; }

        public int Version { get; set; }

        public ContributionBase Contribution { get; set; }
    }
}