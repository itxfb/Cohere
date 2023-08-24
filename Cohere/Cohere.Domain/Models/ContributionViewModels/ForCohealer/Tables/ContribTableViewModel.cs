using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables
{
    public class ContribTableViewModel
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string Title { get; set; }
        public string ServiceProviderName { get; set; }
        public string AuthorAvatarUrl { get; set; }
        public bool IsMembersHiddenInCommunity { get; set; }
        public bool IsWorkshop { get; set; }
        public bool IsInvoiced { get; set; }
        public string Type { get; set; }
        public List<string> PreviewContentUrls { get; set; }
        public string ContributionImage { get; set; }

        public string Status { get; set; }

        public string ReasonDescription { get; set; }

        public decimal EarnedRevenue { get; set; }

        public int StudentsNumber { get; set; }

        public ClosestCohealerSession ClosestSession { get; set; }
        public List<Session> Sessions { get; set; } = new List<Session>();
        public List<ClientModel> Clients { get; set; }
        public PaymentInfoViewModel paymentInfo { get; set; }
        public string PaymentType { set; get; }
        public List<ParticipantViewModel> Participants { get; set; } = new List<ParticipantViewModel>();
        public long UnReadPostCount { get; set; }

        public string Currency { get; set; }
        public string Symbol { get; set; }
        public string Purpose { get; set; }
        public bool ArchivingAllowed { get; set; }
        public bool DeletingAllowed { get; set; }
        public DateTime CreateTime { get; set; }
        public string TimeZoneShortForm { get; set; }
    }
    public class ClientModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AvatarUrl { get; set; }
    }
}
