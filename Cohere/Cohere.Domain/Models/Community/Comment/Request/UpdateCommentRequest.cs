using System.ComponentModel.DataAnnotations;

namespace Cohere.Domain.Models.Community.Comment.Request
{
    public class UpdateCommentRequest : CreateCommentRequest
    {
        [Required]
        public string Id { get; set; }
        public string UserId { get; set; }
    }
}