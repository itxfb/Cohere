using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.Attachment;
using Cohere.Domain.Models.Community.Attachment.Request;
using Cohere.Domain.Models.Community.Post;
using Cohere.Domain.Models.Community.Post.Request;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Entity.Entities.Community;

namespace Cohere.Domain.Service.Abstractions.Community
{
    public interface IPostService
    {
        Task<PostDto> AddAsync(CreatePostRequest model, string accountId);
        Task<PagedPostDto> GetAllForContributionAsync(string contributionId, string accountId, int pageNumber = 1, int pageSize = 10, bool skipPinnedPosts = false);
        Task<PagedPostDto> GetAllForProfileAsync(string profileId, string accountId, int pageNumber = 1, int pageSize = 10);
        Task<PostDto> GetByIdAsync(string postId);
        Task<PostDto> GetProfilePostByIdAsync(string postId);
        Task<PostDto> UpdateAsync(UpdatePostRequest request, string currentAccountId);
        Task<PostDto> DeleteAsync(string postId);
        Task<PostDto> AddAttachmentAsync(AddPostAttachmentRequest request, string accountId, bool isProfilePage = false);
        Task<OperationResult> UploadAttachmentForPostAsync(
            FileDetails fileDetails,
            string postId,
            int partNumber,
            bool isLastPart,
            string documentId, 
            bool isCommenttype);
        Task<PostDto> DeleteAttachmentAsync(string postId, string attachmentId, string accountId, bool isProfilePage = false);
        Task<PostDto> MakethePostStarred(string postId, string accountId, bool isStared, bool isProfilePage = false);
        Task<List<PostDto>> GetAllStarredPost(string userId, bool isProfilePage = false);
        Task<List<PostDto>> GetAllUserTaggedPosts(string userId, string contributionId);
        Task<List<PostDto>> GetAllPinedPostsInContribution(string contributionId, bool isPinned, int? skip, int? take);
        Task<List<PostDto>> GetAllUserTaggedPostsByProfile(string userId, string profileId);
        Task<List<PostDto>> GetPostsUsingKeywordsSearch(string contributionId, string keywords);
        Task<List<PostDto>> GetProfilePostsUsingKeywordsSearch(string profileId, string keywords);
        Task<OperationResult> SaveLastSeenForPosts(string accountId, string contributionId, bool isRead);
        Task<OperationResult> SaveLastSeenForProfilePosts(string accountId, string profileId, bool isRead);
        Task<OperationResult> GetLastSeenPostsCount(string accountId, string contributionId);
        Task<OperationResult> GetLastSeenProfilePostsCount(string accountId, string profileId);
        Task<OperationResult> SaveHashtag(string hashtagText);
        Task<List<string>> GetAllCommunityHashtags(string searchText);
    }
}