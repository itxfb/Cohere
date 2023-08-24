using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models
{
    public class CouponDto
    {
        public string Id { get; set; }

        public string Object { get; set; }

        public long? AmountOff { get; set; }

        public DateTime Created { get; set; }

        public string Currency { get; set; }

        public bool? Deleted { get; set; }

        public string Duration { get; set; }

        public long? DurationInMonths { get; set; }

        public bool Livemode { get; set; }

        public long? MaxRedemptions { get; set; }

        public Dictionary<string, string> Metadata { get; set; }

        public string Name { get; set; }

        public decimal? PercentOff { get; set; }

        public DateTime? RedeemBy { get; set; }

        public long TimesRedeemed { get; set; }

        public bool Valid { get; set; }
    }
}