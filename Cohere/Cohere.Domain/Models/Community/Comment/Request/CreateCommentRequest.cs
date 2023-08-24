using System.Collections.Generic;
using Cohere.Entity.Entities.Facebook;

namespace Cohere.Domain.Models.Community.Comment.Request
{
    public class CreateCommentRequest
    {
        public string PostId { get; set; }
        public string Text { get; set; }
        public string ParentCommentId { get; set; }
        public List<CommunityAttachment> Attachments { get; set; } = new List<CommunityAttachment>();
    }
}