using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class BookOneToOneTimeResultViewModel
    {
        public string ContributionId { get; set; }

        public Dictionary<string, IEnumerable<string>> AvailabilityTimeIdBookedTimeIdPairs { get; set; }
    }
}
