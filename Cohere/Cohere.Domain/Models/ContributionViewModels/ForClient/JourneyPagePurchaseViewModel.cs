using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{

    
    public class JourneyPagePurchaseViewModel
    {
        public string ContributionId { get; set; }

        public string AuthorUserId { get; set; }

        public string AuthorAvatarUrl { get; set; }

        public bool IsAccessRevokedByCoach { set; get; }

        public List<string> PreviewContentUrls { get; set; }

        public string ServiceProviderName { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }

        public float? Rating { get; set; }

        public int? LikesNumber { get; set; }

        public DateTime? PurchaseDateTime { get; set; }

        public List<Session> GroupCourseSessions { get; set; } = new List<Session>();

        public OneToOneSessionDataUi OneToOneSession { get; set; } = new OneToOneSessionDataUi();
        public List<ParticipantViewModel> Participants { get; set; } = new List<ParticipantViewModel>();
        public long UnReadPostCount { get; set; }

        public int PercentageCompleted { get; set; }
        public bool IsUpcoming { get; set; }
        public bool IsPast { get; set; }
        public bool IsMembersHiddenInCommunity { get; set; }
        public bool IsWorkshop { get; set; }
        public DateTime CreateTime { get; set; }
    }
}
