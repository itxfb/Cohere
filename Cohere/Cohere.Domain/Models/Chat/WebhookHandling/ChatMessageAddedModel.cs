using System;

namespace Cohere.Domain.Models.Chat.WebhookHandling
{
    public class ChatMessageAddedModel : ChatEventModel
    {
        public string MessageSid { get; set; }

        public int? Index { get; set; }

        public string Body { get; set; }

        public string Attributes { get; set; }

        public string From { get; set; }

        public DateTime DateCreated { get; set; }
    }
}
