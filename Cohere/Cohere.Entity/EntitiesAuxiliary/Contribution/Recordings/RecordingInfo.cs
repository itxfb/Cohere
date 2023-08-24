using System;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings
{
    public class RecordingInfo
    {
        public string RoomId { get; set; }

        public string RoomName { get; set; }

        public DateTime? DateCreated { get; set; }

        public string CompositionFileName { get; set; }

        public int? Duration { get; set; }

        public RecordingStatus Status { get; set; }

        public override bool Equals(object obj)
        {
            return obj is RecordingInfo info &&
                   RoomId == info.RoomId &&
                   RoomName == info.RoomName &&
                   DateCreated == info.DateCreated &&
                   CompositionFileName == info.CompositionFileName &&
                   Duration == info.Duration &&
                   Status == info.Status;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoomId, RoomName, DateCreated, CompositionFileName, Duration, Status);
        }
    }
}
