namespace Cohere.Domain.Models.Chat.WebhookHandling
{
    public class ChatMediaMessageAddedModel : ChatMessageAddedModel
    {
        public string MediaFilename { get; set; }

        public string MediaContentType { get; set; }

        public string MediaSid { get; set; }

        public int MediaSize { get; set; }
    }
}
