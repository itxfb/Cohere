using System;
using System.Collections.Generic;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class ClosestCohealerSession
    {
        public string ContributionName { get; set; }

        public string ContributionId { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }
        public DateTime StartTime { get; set; }

        public int EnrolledTotal { get; set; }

        public int? EnrolledMax { get; set; }

        public string TimezoneId { get; set; }

        public string ClassId { get; set; }
        public bool IsWorkshop { get; set; }

        public string ClassGroupId { get; set; }

        public string ChatSid { get; set; }
        public bool IsCompleted { get; set; }

        public LiveVideoProvider LiveVideoServiceProvider { get; set; }

        public string ZoomStartUrl { get; set; }
        public bool? IsPrerecorded { get; set; }
        public string PreviewImageUrl { get; set; }
        public List<SessionTime> SessionTimes { get; set; } = new List<SessionTime>();
        public string PaymentType { get; set; }
        public Session GroupSessions { get; set; } = new Session();
        public string TimeZone { get; set; }
        public List<AvailabilityTime> OneToOneSessions { get; set; } = new List<AvailabilityTime>();
    }
}
