using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Cohere.Domain.Models.Community.Post.Request
{
    public class CreatePostRequest
    {
        public string ContributionId { get; set; }
        public string ProfileId { get; set; }
        public string Text { get; set; }
        public bool IsDraft { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsScheduled { get; set; }
        public bool SavedAsDraft { get; set; }
        public bool IsStarred { get; set; }
        public List<string> HashTags { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public List<string> TaggedUserIds { get; set; }

    }
}