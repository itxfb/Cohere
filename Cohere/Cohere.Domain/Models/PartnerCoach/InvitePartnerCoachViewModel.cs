using System.Collections.Generic;

namespace Cohere.Domain.Models.PartnerCoach
{
    public class InvitePartnerCoachViewModel
    {
        public IEnumerable<string> Emails { get; set; }

        public string ContributionId { get; set; }
    }
}
