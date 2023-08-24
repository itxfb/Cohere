using System;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class DeletedBookingWithClientId
    {
        public string ContributionId { get; set; }

        public string ContributionTitle { get; set; }

        public string ClassGroupId { get; set; }

        public string ClassGroupName { get; set; }

        public string ClassId { get; set; }

        public string ParticipantId { get; set; }

        public DateTime DeletedStartTime { get; set; }
        public string ContributionName { get; set; }
        
    }
}
