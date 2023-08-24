using System.Collections.Generic;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class GroupChat
    {
        public string Sid { get; set; }

        public string FriendlyName { get; set; }

        public string PreviewImageUrl { get; set; }
        /// <summary>
        /// Key - UserId, Value = chatSid
        /// </summary>
        public Dictionary<string, string> CohealerPeerChatSids { get; set; } = new Dictionary<string, string>();

        public List<PartnerChats> PartnerChats { get; set; } = new List<PartnerChats>();

        public Dictionary<string, string> PartnerClientChatSid { get; set; } = new Dictionary<string, string>();
    }
}
