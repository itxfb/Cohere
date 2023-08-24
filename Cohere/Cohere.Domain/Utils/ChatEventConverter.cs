using System;
using Cohere.Domain.Models.Chat.WebhookHandling;
using Newtonsoft.Json.Linq;

namespace Cohere.Domain.Utils
{
    public class ChatEventConverter : JsonCreationConverter<ChatEventModel>
    {
        protected override ChatEventModel Create(Type objectType, JObject jObject)
        {
            if (jObject is null)
            {
                throw new ArgumentNullException("jObject");
            }

            switch (jObject["eventType"]?.Value<string>())
            {
                case "onMessageSent":
                    return new ChatMessageAddedModel();

                case "onMediaMessageSent":
                    return new ChatMediaMessageAddedModel();

                case "onMemberUpdated":
                    return new ChatMemberUpdatedModel();

                default:
                    return null;
            }
        }
    }
}