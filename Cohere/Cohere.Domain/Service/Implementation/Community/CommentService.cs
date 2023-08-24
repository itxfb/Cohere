using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.Comment;
using Cohere.Domain.Models.Community.Comment.Request;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.UnitOfWork;

using MongoDB.Driver;

namespace Cohere.Domain.Service.Implementation.Community
{
    public class CommentService : ICommentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILikeService _likeService;
        private readonly IMapper _mapper;
        private readonly IFCMService _fcmService;


        public CommentService(IUnitOfWork unitOfWork, ILikeService likeService, IMapper mapper, IFCMService fcmService)
        {
            _unitOfWork = unitOfWork;
            _likeService = likeService;
            _mapper = mapper;
            _fcmService = fcmService;
        }

        public async Task<CommentDto> AddAsync(CreateCommentRequest request, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);

            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found", request);
            }

            var post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(_ => _.Id == request.PostId);

            if (post == null)
            {
                throw new ValidationException($"Post {request.PostId} not found", request);
            }

            var entity = _mapper.Map<Comment>(request);
            entity.UserId = user.Id;

            var createdComment = await _unitOfWork.GetRepositoryAsync<Comment>().Insert(entity);
            try
            {
                await _fcmService.SendCommentPushNotification(entity);
            }
            catch
            {

            }
            var commentDto = _mapper.Map<CommentDto>(createdComment);
            commentDto.UserInfo = _mapper.Map<CommunityUserDto>(user);

            return commentDto;
        }

        public async Task<ICollection<CommentDto>> GetAllForPostAsync(string postId)
        {
            var post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(_ => _.Id == postId);

            if (post == null)
            {
                throw new ValidationException($"Post {postId} not found");
            }

            var commentsWithUserInfo = await _unitOfWork.GetGenericRepositoryAsync<Comment>().Collection
                .Aggregate()
                .Match(_ => _.PostId == postId)
                .Lookup<Comment, User, Comment>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    comment => comment.UserId, user => user.Id, c => c.UserInfo)
                .Unwind<Comment, Comment>(_ => _.UserInfo)
                .Lookup<Comment, Like, Comment>(_unitOfWork.GetGenericRepositoryAsync<Like>().Collection,
                    c => c.Id, l => l.CommentId, c => c.Likes)
                .ToListAsync();

            commentsWithUserInfo.ForEach((c) => {
                c.Likes?.ToList()?.ForEach((l) =>
                {
                    l.UserInfo = _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.Id == l.UserId).GetAwaiter().GetResult();
                });
            });
            commentsWithUserInfo = commentsWithUserInfo.OrderBy(c => c.CreateTime).ToList();

            List<Comment> sortedComments = new List<Comment>();
            if(commentsWithUserInfo?.Count() > 0)
			{
                sortedComments = commentsWithUserInfo.Where(c => c.ParentCommentId == null)?.ToList();
                foreach(var parentComment in sortedComments)
				{
                    PupulateCommentWithReplyes(parentComment, commentsWithUserInfo, 1);
                }

            }

            return _mapper.Map<ICollection<CommentDto>>(sortedComments);
        }

        private void PupulateCommentWithReplyes(Comment comment, List<Comment> allComments, int ident)
		{
            if(comment != null && allComments?.Count() > 0)
			{
                var replies = allComments.Where(c => c.ParentCommentId == comment.Id);
                if(replies?.Count() > 0)
				{
                    comment.ChildComments = replies;
                    foreach(var reply in comment.ChildComments)
					{
                        reply.Ident = ident;
                        PupulateCommentWithReplyes(reply, allComments, ++ident);

                    }
				}
                else
				{
                    comment.ChildComments = new List<Comment>();
				}

            }
		}

        public async Task<CommentDto> UpdateAsync(UpdateCommentRequest request, string currentAccountId)
        {
            var currentUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == currentAccountId);
            if (currentUser == null)
            {
                throw new ValidationException($"User with {currentAccountId} not found", request);
            }


            if(string.IsNullOrEmpty(request.UserId))
			{
                throw new ValidationException($"Comment with {request.Id} missing a User Id");
            }

            if (!string.IsNullOrEmpty(request.UserId) && currentUser.Id != request.UserId && !currentUser.IsCohealer)
            {
                throw new AccessDeniedException($"User with {currentAccountId} can't edit comments of others");
            }

            var entity = await _unitOfWork.GetRepositoryAsync<Comment>().GetOne(p => p.Id == request.Id);
            if (entity == null)
            {
                throw new ValidationException($"Comment with {request.Id} not found");
            }

            entity = _mapper.Map(request, entity);

            var updatedComment = await _unitOfWork.GetRepositoryAsync<Comment>().Update(entity.Id, entity);

            var commentDto = _mapper.Map<CommentDto>(updatedComment);
            // TODO: Check can I get info about user with getting comment
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.Id == commentDto.UserId);

            commentDto.UserInfo = _mapper.Map<CommunityUserDto>(user);
            commentDto.Likes = await _likeService.GetAllForCommentAsync(commentDto.Id);

            return commentDto;
        }

        public async Task<CommentDto> DeleteAsync(string commentId)
        {
            var entity = await _unitOfWork.GetRepositoryAsync<Comment>().GetOne(p => p.Id == commentId);
            if (entity == null)
            {
                throw new ValidationException($"Comment with {commentId} not found");
            }

            await _unitOfWork.GetRepositoryAsync<Comment>().Delete(entity);

            return _mapper.Map<CommentDto>(entity);
        }
    }
}