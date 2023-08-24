using System;

namespace Cohere.Domain.Models.Chat.WebhookHandling
{
    public class ChatMemberUpdatedModel : ChatEventModel
    {
        public string MemberSid { get; set; }

        public string Identity { get; set; }

        public string RoleSid { get; set; }

        public string Source { get; set; }

        public int? LastConsumedMessageIndex { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}
