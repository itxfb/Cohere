using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyClassViewModel
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public List<string> PreviewContentUrls { get; set; }

        public string ServiceProviderName { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }

        public int? TotalNumberSessions { get; set; }

        public int PercentageCompleted { get; set; }

        public float? Rating { get; set; }

        public int? LikesNumber { get; set; }

        public DateTime? PurchaseDateTime { get; set; }

        public DateTime? SessionTime { get; set; }

        public int? NumberCompletedSessions { get; set; }
        public string SessionId { get; set; }
        public bool IsPrerecorded { get; set; }
        public bool? IsCompleted { get; set; }
        public string TimezoneId { get; set; }
        public SessionTime SessionTimes { get; set; }
        public bool IsWorkshop { get; set; }
        public string TimeZoneShortForm { get; set; }
    }
}
