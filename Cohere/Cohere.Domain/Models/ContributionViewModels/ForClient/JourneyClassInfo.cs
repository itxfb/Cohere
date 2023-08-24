using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class JourneyClassInfo
    {
        public string ContributionId { get; set; }

        public string ClassId { get; set; }

        public string AuthorUserId { get; set; }

        public List<string> PreviewContentUrls { get; set; }

        public string Type { get; set; }

        public string ContributionTitle { get; set; }

        public int? TotalNumberSessions { get; set; }

        public int PercentageCompleted { get; set; }
        public float? Rating { get; set; }

        public int? LikesNumber { get; set; }

        public DateTime? SessionTimeUtc { get; set; }

        public int? NumberCompletedSessions { get; set; }
        public bool IsPrerecorded { get; set; }
        public string SessionId { get; set; }
        public string SessionTitle { get; set; }
        public string TimezoneId { get; set; }
        public SessionTime SessionTimes { get; set; }
        public List<Session> GroupSessions { get; set; }
        public List<AvailabilityTime> OneToOneSessions { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsWorkshop { get; set; }
        public string AuthorName { get; set; }
        public DateTime? UpComingSesionTime { get; set; }
        public DateTime? InCompletedSesionTime { get; set; }
        public DateTime? PastSesionTime { get; set; }
        public string AuthorAvatarUrl { get; set; }
        public bool IsAccessRevokedByCoach { get; set; }
        public string TimeZoneShortForm { get; set; }
    }
}
