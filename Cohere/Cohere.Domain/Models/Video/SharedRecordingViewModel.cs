using System;

namespace Cohere.Domain.Models.Video
{
    public class SharedRecordingViewModel
    {
        public string ContributionId { get; set; }
        public string SessionTimeId { get; set; }
        public string PassCode { get; set; }
        public bool IsPassCodeEnabled { get; set; }
    }
}
