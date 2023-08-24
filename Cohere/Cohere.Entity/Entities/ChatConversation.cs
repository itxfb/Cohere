using System;
using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Chat;
using Cohere.Entity.Enums;

namespace Cohere.Entity.Entities
{
    public class ChatConversation : BaseEntity
    {
        public string ChatSid { get; set; }

        public ChatTypes ChatType { get; set; }

        public int? LastMessageIndex { get; set; }

        public string LastMessageAuthorUserId { get; set; }

        public DateTime LastMessageAddedTimeUtc { get; set; }

        public bool HasUnread { get; set; }
        public bool IsPinned { get; set; }

        public List<ChatUserReadInfo> UserReadInfos { get; set; } = new List<ChatUserReadInfo>();
    }
}
