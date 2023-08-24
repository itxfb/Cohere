using System;
using System.Collections.Generic;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ClosestClassForBannerViewModel
    {
        public string AuthorUserId { get; set; }

        public string ContributionId { get; set; }

        public string ContributionTitle { get; set; }

        public string ContributionType { get; set; }

        public string ClassGroupId { get; set; }

        public string ClassId { get; set; }

        public string Title { get; set; }

        public int MinutesLeft { get; set; }

        public int PercentageCompleted { get; set; }

        public string OneToOneParticipantId { get; set; }

        public bool IsRunning { get; set; }

        public string ChatSid { get; set; }

        public DateTime StartTime { get; set; }

        public LiveVideoProvider ContributionLiveVideoServiceProvider { get; set; }

        public string ZoomStartUrl { get; set; }
        public bool? IsPrerecorded { get; set; }
        public List<SessionTime> SessionTimes { get; set; } = new List<SessionTime>();

    }
}
