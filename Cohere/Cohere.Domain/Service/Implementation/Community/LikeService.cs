using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.Like.Request;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.UnitOfWork;

using MongoDB.Driver;

namespace Cohere.Domain.Service.Implementation.Community
{
    public class LikeService : ILikeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFCMService _fcmService;
        private readonly IMapper _mapper;

        public LikeService(IUnitOfWork unitOfWork, IMapper mapper, IFCMService fcmService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _fcmService = fcmService;
        }

        public async Task<LikeDto> AddAsync(AddLikeRequest request, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);

            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found", request);
            }

            if (!string.IsNullOrEmpty(request.PostId))
            {
                var post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(_ => _.Id == request.PostId);

                if (post == null)
                {
                    throw new ValidationException($"Post {request.PostId} not found", request);
                }
            }

            if (!string.IsNullOrEmpty(request.CommentId))
            {
                var comment = await _unitOfWork.GetRepositoryAsync<Comment>().GetOne(_ => _.Id == request.CommentId);

                if (comment == null)
                {
                    throw new ValidationException($"Comment {request.CommentId} not found", request);
                }
            }

            var existingLike = await _unitOfWork.GetRepositoryAsync<Like>().GetOne(_ => _.PostId == request.PostId && _.CommentId == request.CommentId && _.UserId == user.Id);
            if(existingLike != null)
			{
                throw new ValidationException($"Like already exists", request);
            }

            var entity = _mapper.Map<Like>(request);
            entity.UserId = user.Id;
            var createdLike = await _unitOfWork.GetRepositoryAsync<Like>().Insert(entity);

            try
            {
                await _fcmService.SendLikePushNotification(entity);
            }
            catch
            {

            }

            var likeDto = _mapper.Map<LikeDto>(createdLike);
            likeDto.UserInfo = _mapper.Map<CommunityUserDto>(user);

            return likeDto;
        }

        public async Task<ICollection<LikeDto>> GetAllForPostAsync(string postId)
        {
            var post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(_ => _.Id == postId);

            if (post == null)
            {
                throw new ValidationException($"Post {postId} not found");
            }

            var likesWithUserInfo = await _unitOfWork.GetGenericRepositoryAsync<Like>().Collection
                .Aggregate()
                .Match(_ => _.PostId == postId)
                .Lookup<Like, User, Like>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection, like => like.UserId,
                    user => user.Id, result => result.UserInfo)
                .Unwind<Like, Like>(_ => _.UserInfo)
                .ToListAsync();

            likesWithUserInfo = likesWithUserInfo?.Where(l => l.CommentId == null)?.ToList();

            return _mapper.Map<ICollection<LikeDto>>(likesWithUserInfo);
        }

        public async Task<ICollection<LikeDto>> GetAllForCommentAsync(string commentId)
        {
            var comment = await _unitOfWork.GetRepositoryAsync<Comment>().GetOne(_ => _.Id == commentId);

            if (comment == null)
            {
                throw new ValidationException($"Comment {commentId} not found");
            }

            var likesWithUserInfo = await _unitOfWork.GetGenericRepositoryAsync<Like>().Collection
                .Aggregate()
                .Match(_ => _.CommentId == commentId)
                .Lookup<Like, User, Like>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection, like => like.UserId,
                    user => user.Id, result => result.UserInfo)
                .Unwind<Like, Like>(_ => _.UserInfo)
                .ToListAsync();

            return _mapper.Map<ICollection<LikeDto>>(likesWithUserInfo);
        }

        public async Task<LikeDto> DeleteAsync(string likeId)
        {
            var entity = await _unitOfWork.GetRepositoryAsync<Like>().GetOne(p => p.Id == likeId);
            if (entity == null)
            {
                throw new ValidationException($"Like {likeId} not found");
            }

            await _unitOfWork.GetRepositoryAsync<Like>().Delete(entity);

            return _mapper.Map<LikeDto>(entity);
        }
    }
}