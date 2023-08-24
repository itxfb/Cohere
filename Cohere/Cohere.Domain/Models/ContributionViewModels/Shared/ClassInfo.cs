using System;
using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ClassInfo
    {
        public bool IsCompleted { get; set; }

        public List<string> ParticipantIds { get; set; }

        public DateTime StartTime { get; set; }

        public IVideoRoomContainer VideoRoomContainer { get; set; }

        public List<RecordingInfo> RecordingInfos { get; set; }
    }
}