using System;

namespace Cohere.Domain.Models.ContributionViewModels.ForAdmin
{
    public class ContributionAdminBriefViewModel
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string TimeZoneId { get; set; }

        public string ServiceProviderName { get; set; }

        public string Title { get; set; }

        public string Status { get; set; }

        public string Type { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }
    }
}
