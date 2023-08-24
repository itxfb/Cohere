using System.Collections.Generic;

namespace Cohere.Domain.Service.Users
{
    public class UserPreviewViewModel
    {
        public string Id { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string AvatarUrl { get; set; }
        public string Sid { get; set; }
        public string FriendlyName { get; set; }
        public Dictionary<string, string> CohealerPeerChatSids { get; set; } = new Dictionary<string, string>();
    }
}