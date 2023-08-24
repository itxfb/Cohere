using Cohere.Entity.EntitiesAuxiliary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities.Chat
{
    public class CustomerSupportChat : BaseEntity
    {
        public string Sid { get; set; }
        public List<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    }
}
