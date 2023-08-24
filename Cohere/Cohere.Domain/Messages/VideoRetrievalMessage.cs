using System;

namespace Cohere.Domain.Messages
{
    public class VideoRetrievalMessage
    {
        public string ContributionId { get; set; }

        public string RoomId { get; set; }

        public string CompositionId { get; set; }

        public DateTime TimeOfRecording { get; set; }
    }
}
