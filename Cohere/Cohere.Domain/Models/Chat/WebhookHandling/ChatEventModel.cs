using Cohere.Domain.Utils;

namespace Cohere.Domain.Models.Chat.WebhookHandling
{
    [Newtonsoft.Json.JsonConverter(typeof(ChatEventConverter))]
    public class ChatEventModel
    {
        public string AccountSid { get; set; }

        public string InstanceSid { get; set; }

        public string ChannelSid { get; set; }

        public string ClientIdentity { get; set; }

        public string EventType { get; set; }
    }
}
