using System;

namespace Cohere.Domain.Models.Video
{
    public class TwilioCompositionWebHookModel
    {
        public string RoomSid { get; set; }

        public string CompositionSid { get; set; }

        public string StatusCallbackEvent { get; set; }

        public DateTime Timestamp { get; set; }

        public string AccountSid { get; set; }

        public string CompositionUri { get; set; }

        public string MediaUrl { get; set; }

        public long Duration { get; set; }

        public long Size { get; set; }
    }
}
