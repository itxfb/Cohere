namespace Cohere.Domain.Models.Community.Like.Request
{
    public class AddLikeRequest
    {
        public string PostId { get; set; }
        public string CommentId { get; set; }
    }
}