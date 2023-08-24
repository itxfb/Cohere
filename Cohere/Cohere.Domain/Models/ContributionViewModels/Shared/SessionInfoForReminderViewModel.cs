using System;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class SessionInfoForReminderViewModel
    {
        public string AuthorUserId { get; set; }

        public string ClientUserId { get; set; }

        public string ContributionId { get; set; }

        public string ContributionTitle { get; set; }

        public string ClassId { get; set; }

        public DateTime ClassStartTimeUtc { get; set; }
    }
}
