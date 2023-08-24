using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels
{
    public class ShareContributionEmailViewModel
    {
        public string ContributionId { get; set; }

        public List<string> EmailAddresses { get; set; }
    }
}
