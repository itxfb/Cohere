using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class ContributionInCohealerInfoViewModel
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public List<string> PreviewContentUrls { get; set; }

        public float Rating { get; set; }

        public string Type { get; set; }

        public bool IsMeEnrolled { get; set; }

        public bool InvitationOnly { get; set; }
    }
}
