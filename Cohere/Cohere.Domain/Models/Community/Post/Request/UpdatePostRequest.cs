using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Cohere.Entity.Entities.Facebook;

namespace Cohere.Domain.Models.Community.Post.Request
{
    public class UpdatePostRequest : CreatePostRequest
    {
        [Required]
        public string Id { get; set; }
        public string UserId { get; set; }
        public bool IsPinned { get; set; }
        public bool IsBubbled { get; set; }
        public bool IsFlagged { get; set; }
        public IEnumerable<CommunityAttachment> Attachments { get; set; }
    }
}