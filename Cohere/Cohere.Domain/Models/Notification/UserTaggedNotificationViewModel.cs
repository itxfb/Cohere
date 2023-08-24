using System.Collections.Generic;

namespace Cohere.Domain.Models.Notification
{
    public class UserTaggedNotificationViewModel
    {
        public string MentionAuthorUserName { get; set; }
        public string AuthorUserId { get; set; }
        public List<string> MentionedUserIds { get; set; }
        public string ContributionName { get; set; }
        public string ContributionId { get; set; }
        public string PostId { get; set; }
        public string ReplyLink { get; set; }
        public string Message { get; set; }
    }
}
