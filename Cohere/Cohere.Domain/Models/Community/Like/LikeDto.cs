using System.Text.Json.Serialization;
using Cohere.Domain.Models.Community.UserInfo;

namespace Cohere.Domain.Models.Community.Like
{
    public class LikeDto
    {
        public string Id { get; set; }
        [JsonIgnore]
        public string UserId { get; set; }
        public string PostId { get; set; }
        public string CommentId { get; set; }
        public CommunityUserDto UserInfo { get; set; }
    }
}