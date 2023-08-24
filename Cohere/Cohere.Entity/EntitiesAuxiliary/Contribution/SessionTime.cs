using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class SessionTime : TimeRange, IVideoRoomContainer
    {
        public string Id { get; set; }

        public VideoRoomInfo VideoRoomInfo { get; set; }

        public List<RecordingInfo> RecordingInfos { get; set; } = new List<RecordingInfo>();

        public List<ParticipantInfo> ParticipantInfos { get; set; }

        public List<string> ParticipantsIds { get; set; } = new List<string>();

        public List<EventInfo> EventInfos { get; set; } = new List<EventInfo>();

        public string ScheduledNotficationJobId { get; set; }

        public string PodId { get; set; }

        public bool IsCompleted { get; set; }

        public List<string> CompletedSelfPacedParticipantIds { get; set; } = new List<string>();

        public DateTime? CompletedDateTime { get; set; }

        public Document PrerecordedSession { get; set; }
        public List<Document> Attachments { get; set; } = new List<Document>();
        public string MoreInfo { get; set; }

        //public string SubCategoryName { get; set; }
        private string subCat;
        public string SubCategoryName
        {
            get { return subCat; }
            set
            {
                if (!string.IsNullOrEmpty(value) && value.Length > 50)
                {
                    subCat = value.Substring(0, 49);
                }
                else
                    subCat = value;
            }
        }

        public ZoomMeetingData ZoomMeetingData { get; set; }

        public bool IgnoreDateAvailable { get; set; }

        public bool MustWatchPriorSelfPacedRecords { get; set; }

        public List<string> UsersWhoViewedRecording { get; set; } = new List<string>();

        public override bool Equals(object obj)
        {
            return obj is SessionTime time &&
                   Id == time.Id &&
                   VideoRoomInfo.Equals(VideoRoomInfo, time.VideoRoomInfo) &&
                   RecordingInfos.All(n => time.RecordingInfos.Contains(n)) &&
                   RecordingInfos.Count == time.RecordingInfos.Count &&
                   ParticipantsIds.All(n => time.ParticipantsIds.Contains(n)) &&
                   ParticipantsIds.Count == time.ParticipantsIds.Count &&
                   PodId == time.PodId &&
                   IsCompleted == time.IsCompleted &&
                   CompletedDateTime == time.CompletedDateTime;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, VideoRoomInfo, RecordingInfos, ParticipantsIds, IsCompleted, CompletedDateTime, PodId);
        }

        public string PassCode { get; set; }

        public bool IsPassCodeEnabled { get; set; } = false;
    }
}
