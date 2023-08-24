using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Entity.Entities.Facebook;

namespace Cohere.Domain.Models.Community.Comment
{
    public class CommentDto
    {
        public string Id { get; set; }
        public string PostId { get; set; }
        [JsonIgnore]
        public string UserId { get; set; }
        public string Text { get; set; }
        public string ParentCommentId { get; set; }
        public IEnumerable<CommentDto> ChildComments { get; set; }
        public IEnumerable<CommunityAttachment> Attachments { get; set; }
        public IEnumerable<LikeDto> Likes { get; set; }
        public CommunityUserDto UserInfo { get; set; }
        public DateTime CreateTime { get; set; }
        public virtual int Ident { get; set; }
    }
}