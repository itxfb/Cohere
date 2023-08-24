using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.Payment
{
    public class ContributionPaymentIntentDetailsViewModel
    {
        public string Currency { get; set; }

        public decimal? Price { get; set; }

        public decimal? PlatformFee { get; set; }

        public string ClientSecret { get; set; }

        public string Status { get; set; }

        public int? SessionLifeTimeSeconds { get; set; }

        public List<string> BookedTimeIds { get; set; }

        public DateTime Created { get; set; }
    }
}
