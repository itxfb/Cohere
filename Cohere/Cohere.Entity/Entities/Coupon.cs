using System;
using System.Collections.Generic;

namespace Cohere.Entity.Entities
{
    public class Coupon : BaseEntity
    {
        public string CoachId { get; set; }

        public string Duration { get; set; }

        public long? DurationInMonths { get; set; }

        public string Currency { get; set; }

        public long? MaxRedemptions { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        public IEnumerable<string> AllowedContributionTypes { get; set; }

        public string Name { get; set; }

        public decimal? PercentOff { get; set; }

        public long? AmountOff { get; set; }

        public DateTime? RedeemBy { get; set; }

        public long TimesRedeemed { get; set; }
        public string PaymentType { get; set; }
    }
}