using System;

namespace Cohere.Entity.EntitiesAuxiliary.Chat
{
    public class ChatUserReadInfo
    {
        public string Email { get; set; }

        public int? LastReadMessageIndex { get; set; }

        public DateTime? LastReadMessageTimeUtc { get; set; }

        public DateTime FirstNotificationSentUtc { get; set; }

        public DateTime SecondNotificationSentUtc { get; set; }
    }
}
