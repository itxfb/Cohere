using System;
using System.Collections.Generic;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Facebook;

namespace Cohere.Entity.Entities.Community
{
    public class Post : BaseEntity
    {
        public string ContributionId { get; set; }
        public string ProfileId { get; set; } //Profile's Coach User ID
        public ContributionBase Contribution { get; set; }
        public ProfilePage ProfilePage { get; set; }
        public string UserId { get; set; }
        public User UserInfo { get; set; }
        public string Text { get; set; }
        public bool IsDraft { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsPinned { get; set; }
        public DateTime PinnedTime { get; set; }
        public bool IsBubbled { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsStarred { get; set; }
        public bool IsScheduled { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public string ScheduledJobId { get; set; }
        public bool SavedAsDraft { get; set; }
        public List<string> HashTags { get; set; }
        public IEnumerable<CommunityAttachment> Attachments { get; set; } = new List<CommunityAttachment>();
        public List<string> TaggedUserIds { get; set; }
        public string ReplyLink { get; set; }

    }
}