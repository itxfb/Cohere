using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Cohere.Domain.Models.Community.Comment;
using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Facebook;

namespace Cohere.Domain.Models.Community.Post
{
    public class PostDto
    {
        public string Id { get; set; }
        public string ContributionId { get; set; }

        [JsonIgnore]
        public string UserId { get; set; }
        public CommunityPostUserDto UserInfo { get; set; }
        public string Text { get; set; }
        public IEnumerable<CommunityAttachment> Attachments { get; set; }
        public bool IsDraft { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsPinned { get; set; }
        public DateTime PinnedTime { get; set; }
        public bool IsBubbled { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsStarred { get; set; }
        public IEnumerable<Link> Links { get; set; }
        public IEnumerable<LikeDto> Likes { get; set; }
        public IEnumerable<CommentDto> Comments { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsScheduled { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public string ScheduledJobId { get; set; }
        public bool SavedAsDraft { get; set; }
        public List<string> HashTags { get; set; }
        public List<string> TaggedUserIds { get; set; }
        public string ProfileId { get; set; }

    }
}