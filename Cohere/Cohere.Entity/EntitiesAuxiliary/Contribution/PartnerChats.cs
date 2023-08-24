using System.Collections.Generic;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class PartnerChats
    {
        public string PartnerUserId { get; set; }

        public List<PartnerPeerChat> PeerChats { get; set; } = new List<PartnerPeerChat>();
    }
}
