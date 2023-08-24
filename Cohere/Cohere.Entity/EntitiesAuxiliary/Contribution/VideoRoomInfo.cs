using System;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class VideoRoomInfo
    {
        public string RoomId { get; set; }

        public string RoomName { get; set; }

        public bool IsRunning { get; set; }

        public bool RecordParticipantsOnConnect { get; set; }

        public string CreatorId { get; set; }

        public DateTime? DateCreated { get; set; }

        public override bool Equals(object obj)
        {
            return obj is VideoRoomInfo info &&
                   RoomId == info.RoomId &&
                   RoomName == info.RoomName &&
                   IsRunning == info.IsRunning &&
                   RecordParticipantsOnConnect == info.RecordParticipantsOnConnect &&
                   DateCreated == info.DateCreated;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoomId, RoomName, IsRunning, RecordParticipantsOnConnect, DateCreated);
        }
    }
}
