using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary;

namespace Cohere.Entity.Entities
{
    public class PeerChat : BaseEntity
    {
        public string Sid { get; set; }
        public bool IsOpportunity { get; set; }
        public List<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    }
}
