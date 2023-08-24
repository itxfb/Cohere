using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Models.Community.Comment;
using Cohere.Domain.Models.Community.Comment.Request;

namespace Cohere.Domain.Service.Abstractions.Community
{
    public interface ICommentService
    {
        Task<CommentDto> AddAsync(CreateCommentRequest request, string accountId);
        Task<ICollection<CommentDto>> GetAllForPostAsync(string postId);
        Task<CommentDto> UpdateAsync(UpdateCommentRequest request, string currentAccountId);
        Task<CommentDto> DeleteAsync(string commentId);
    }
}