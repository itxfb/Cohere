using System.Collections.Generic;

namespace Cohere.Api.Controllers.Models
{
    public class UnReadPostCountModel
    {
        public string UserId { get; set; }

        public IEnumerable<string> ContributionIds { get; set; }
    }
}
