using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.Like.Request;

namespace Cohere.Domain.Service.Abstractions.Community
{
    public interface ILikeService
    {
        Task<LikeDto> AddAsync(AddLikeRequest request, string accountId);
        Task<ICollection<LikeDto>> GetAllForPostAsync(string postId);
        Task<ICollection<LikeDto>> GetAllForCommentAsync(string commentId);
        Task<LikeDto> DeleteAsync(string likeId);
    }
}