using System;

namespace Cohere.Domain.Models.Video
{
    public class TwilioVideoWebHookModel
    {
        public string RoomStatus { get; set; }

        public string RoomType { get; set; }

        public string RoomSid { get; set; }

        public string RoomName { get; set; }

        public int SequenceNumber { get; set; }

        public string StatusCallbackEvent { get; set; }

        public DateTime Timestamp { get; set; }

        public string AccountSid { get; set; }
    }
}
