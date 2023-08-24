using System.Collections.Generic;
using System.Linq;

namespace Cohere.Domain.Models.ContributionViewModels
{
    public class AcademyContributionPreviewViewModel
    {
        public string Id { get; set; }

        public string Type { get; set; }

        public string Title { get; set; }

        public string UserId { get; set; }
        
        public string AvatarUrl { get; set; }

        public List<string> PreviewContentUrls { get; set; }

        public string Image => PreviewContentUrls?.FirstOrDefault();

        public string ServiceProviderName { get; set; }

        public List<string> Categories { get; set; }
    }
}