namespace Cohere.Domain.Messages
{
    public class VideoCompletedMessage
    {
        public string ContributionId { get; set; }

        public string RoomId { get; set; }

        public string CompositionFileName { get; set; }

        public int? CompositionDuration { get; set; }
    }
}
