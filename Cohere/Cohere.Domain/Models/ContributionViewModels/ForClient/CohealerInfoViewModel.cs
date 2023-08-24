using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class CohealerInfoViewModel
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string NameSuffix { get; set; }

        public string AvatarUrl { get; set; }

        public string City { get; set; }

        public string CountryCode { get; set; }

        public string Bio { get; set; }

        public string LanguageCode { get; set; }

        public string BusinessName { get; set; }

        public string BusinessType { get; set; }

        public string Certification { get; set; }

        public string Occupation { get; set; }

        public List<string> ContributionCategories { get; set; }

        public double AvgContributionsRating { get; set; }

        public List<ContributionInCohealerInfoViewModel> ContributionInfos { get; set; }
        public List<string> CoachChatIds { get; set; }
    }
}
