using System.Collections.Generic;

namespace Cohere.Domain.Models.Affiliate
{
    public class InviteEmailsRequestModel
    {
        public IEnumerable<string> EmailAddresses { get; set; }
    }
}
