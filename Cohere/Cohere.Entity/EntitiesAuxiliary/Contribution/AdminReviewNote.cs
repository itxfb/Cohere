using System;
using Cohere.Entity.Enums.Contribution;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class AdminReviewNote
    {
        public string UserId { get; set; }

        public string Description { get; set; }

        public DateTime DateUtc { get; set; }

        public ContributionStatuses Status { get; set; }
    }
}
