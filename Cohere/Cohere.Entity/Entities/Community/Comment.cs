using Cohere.Entity.Entities.Facebook;
using System.Collections.Generic;

namespace Cohere.Entity.Entities.Community
{
    public class Comment : BaseEntity
    {
        public string PostId { get; set; }
        public string UserId { get; set; }
        public string Text { get; set; }
        public IEnumerable<Like> Likes { get; set; }
        public User UserInfo { get; set; }
        public string ParentCommentId { get; set; }
        public IEnumerable<Comment> ChildComments { get; set; }
        public virtual int Ident { get; set; }
        public List<CommunityAttachment> Attachments { get; set; }
    }
}