using System;
using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class BookedTime : TimeRange, IVideoRoomContainer
    {
        public string Id { get; set; }

        public int SessionIndex { get; set; }

        public VideoRoomInfo VideoRoomInfo { get; set; }

        public List<RecordingInfo> RecordingInfos { get; set; } = new List<RecordingInfo>();

        public string ParticipantId { get; set; }

        public string CalendarEventID { get; set; }
        public string CalendarId { get; set; }

        public EventInfo EventInfo { get; set; }

        public bool IsPurchaseConfirmed { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime? CompletedDateTime { get; set; }

        public List<Document> Attachments { get; set; } = new List<Document>();

        public ZoomMeetingData ZoomMeetingData { get; set; }// Zoom object

        public override bool Equals(object obj)
        {
            return obj is BookedTime time &&
                   Id == time.Id &&
                   SessionIndex == time.SessionIndex &&
                   VideoRoomInfo.Equals(VideoRoomInfo, time.VideoRoomInfo) &&
                   RecordingInfos.All(n => time.RecordingInfos.Contains(n)) &&
                   RecordingInfos.Count == time.RecordingInfos.Count &&
                   ParticipantId == time.ParticipantId &&
                   IsPurchaseConfirmed == time.IsPurchaseConfirmed &&
                   IsCompleted == time.IsCompleted &&
                   CompletedDateTime == time.CompletedDateTime;
        }

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            hash.Add(Id);
            hash.Add(SessionIndex);
            hash.Add(VideoRoomInfo);
            hash.Add(RecordingInfos);
            hash.Add(ParticipantId);
            hash.Add(IsPurchaseConfirmed);
            hash.Add(IsCompleted);
            hash.Add(CompletedDateTime);
            return hash.ToHashCode();
        }
    }
}
