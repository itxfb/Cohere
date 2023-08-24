namespace Cohere.Entity.Entities.Community
{
    public class Like : BaseEntity
    {
        public string PostId { get; set; }
        public string CommentId { get; set; }
        public string UserId { get; set; }
        public User UserInfo { get; set; }
        public Comment Comment { get; set; }
    }
}