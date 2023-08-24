using System;

namespace Cohere.Entity.EntitiesAuxiliary
{
    public class ChatParticipant
    {
        public string UserId { get; set; }

        public string MemberSid { get; set; }

        public bool IsLeft { get; set; }

        public DateTime DateTimeLeft { get; set; }
    }
}
