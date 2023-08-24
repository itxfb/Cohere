using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ContributionPurchaseModel 
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AvatarUrl { get; set; }
        public string Title { get; set; }
        public decimal EarnedRevenue { get; set; }
        public DateTime? CreateTime { get; set; }
        public string ContributionId { get; set; }
        public string DefaultSymbol { get; set; }
        public string Sid { get; set; }
        public string ClientId { get; set; }
        public string FriendlyName { get; set; }
        public Dictionary<string, string> CohealerPeerChatSids { get; set; } = new Dictionary<string, string>();
    }
}
