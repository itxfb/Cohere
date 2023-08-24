using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;

using AutoMapper;
using Castle.Core.Internal;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.Attachment.Request;
using Cohere.Domain.Models.Community.Post;
using Cohere.Domain.Models.Community.Post.Request;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Entity;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Facebook;
using Cohere.Entity.EntitiesAuxiliary.User;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Infrastructure.Extensions;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Cohere.Domain.Service.Implementation.Community
{
    // TODO: merge dev in branch and add services to DI
    // TODO: implement validation in separate method
    public class PostService : IPostService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommentService _commentService;
        private readonly ILikeService _likeService;
        private readonly IAccountManager _accountManager;
        private readonly IFileStorageManager _fileStorageManager;
        private readonly StoragePathTemplatesSettings _storagePathTemplatesSettings;
        private readonly S3Settings _s3SettingsOptions;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _memoryCache;
        private readonly ICommonService _commonService;

        public PostService(
            IUnitOfWork unitOfWork,
            ICommentService commentService,
            ILikeService likeService,
            IAccountManager accountManager,
            IFileStorageManager fileStorageManager,
            IOptions<StoragePathTemplatesSettings> storagePathTemplatesSettings,
            IOptions<S3Settings> s3SettingsOptions,
            IMapper mapper,
            IMemoryCache memoryCache,
            ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _commentService = commentService;
            _likeService = likeService;
            _fileStorageManager = fileStorageManager;
            _accountManager = accountManager;
            _storagePathTemplatesSettings = storagePathTemplatesSettings?.Value;
            _s3SettingsOptions = s3SettingsOptions?.Value;
            _mapper = mapper;
            _memoryCache = memoryCache;
            _commonService = commonService;
        }

        public async Task<PostDto> AddAsync(CreatePostRequest request, string accountId)
        {
            PostDto postDto = new PostDto();
            ContributionBase contribution = null;
            ProfilePage profile = null;

            var entity = _mapper.Map<Post>(request);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);
            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found", request);
            }
            //post against profileId
            if (request.ProfileId != null)
            {
                profile = await _unitOfWork.GetGenericRepositoryAsync<ProfilePage>().GetOne(a => a.UserId == request.ProfileId);

                if (profile == null)
                {
                    throw new ValidationException($"Profile with {request.ProfileId} not found", request);
                }
            }
            //post against contributionId
            else
            {
                contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                    .GetOne(c => c.Id == request.ContributionId);
                if (contribution == null)
                {
                    throw new ValidationException($"Contribution {request.ContributionId} not found", request);
                }
                // TODO: Check if the user against the contribution is cohealer
            }

            entity.UserId = user.Id;
            entity.IsPrivate = entity.IsDraft || entity.IsPrivate;
            var checkDraftPost = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(m => m.UserId == user.Id && m.SavedAsDraft == true);
            if (checkDraftPost == null)
            {
                var createdPost = await _unitOfWork.GetRepositoryAsync<Post>().Insert(entity);
                postDto = _mapper.Map<PostDto>(createdPost);
            }
            else
            {
                postDto = _mapper.Map<PostDto>(checkDraftPost);
            }
            // TODO: Check getting user info with info about post
            postDto.UserInfo = _mapper.Map<CommunityPostUserDto>(user);
            postDto.Links = await GetCachedLinks(postDto);
            postDto.UserInfo.IsCohealer = IsCohealer(user, contribution);
            return postDto;
        }
        public async Task<PagedPostDto> GetAllForContributionAsync(string contributionId, string accountId, int pageNumber = 1, int pageSize = 10, bool skipPinnedPosts = false)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);
            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found");
            }

            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .GetOne(c => c.Id == contributionId);
            if (contribution == null)
            {
                throw new ValidationException($"Contribution {contributionId} not found");
            }

            var postsFilter = await GetFilterForPosts(user, contribution);
            var postsCollection = _unitOfWork.GetGenericRepositoryAsync<Post>().Collection;

            var (totalPages, data) = await postsCollection.AggregateByPage(
                postsFilter,
                Builders<Post>.Sort.Descending(x => x.PinnedTime).Descending(x => x.CreateTime),
                pageNumber,
                pageSize);

            var draftPostWithUserInfo = await postsCollection
                .Aggregate()
                .Match(_ => _.ContributionId == contributionId && _.IsDraft && _.UserId == user.Id)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(_ => _.UserInfo)
                .FirstOrDefaultAsync();

            var draftPostDto = _mapper.Map<PostDto>(draftPostWithUserInfo);
            if (draftPostDto != null)
            {
                draftPostDto.UserInfo.IsCohealer = IsCohealer(user, contribution);
            }

            var postDtos = _mapper.Map<ICollection<PostDto>>(data);
            var filteredPosts = new List<PostDto>();

            if (skipPinnedPosts)
            {
                filteredPosts = postDtos.Where(m => !m.IsPinned).ToList();
            }
            else
            {
                filteredPosts = postDtos.ToList();
            }
            var postDtosWithSubItems = new List<PostDto>();

            foreach (var postDto in filteredPosts)
            {
                var post = await FillSubItemsForPost(postDto);
                var postUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.Id == postDto.UserId);
                postDto.UserInfo = _mapper.Map<CommunityPostUserDto>(postUser);
                postDto.UserInfo.IsCohealer = IsCohealer(postUser, contribution);
                postDtosWithSubItems.Add(post);
            }

            return new PagedPostDto
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                Posts = postDtosWithSubItems,
                UserDraftPost = await FillSubItemsForPost(draftPostDto)
            };
        }
        public async Task<PagedPostDto> GetAllForProfileAsync(string profileId, string accountId, int pageNumber = 1, int pageSize = 10)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);
            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found");
            }
            var profile = await _unitOfWork.GetRepositoryAsync<ProfilePage>()
                .GetOne(c => c.UserId == profileId);
            if (profile == null)
            {
                throw new ValidationException($"Profile with {profileId} not found");
            }
            var postsFilter = GetFilterForProfilePosts(user, profileId);
            var postsCollection = _unitOfWork.GetGenericRepositoryAsync<Post>().Collection;
            var (totalPages, data) = await postsCollection.AggregateByPage(
                postsFilter,
                Builders<Post>.Sort.Descending(x => x.PinnedTime).Descending(x => x.CreateTime),
                pageNumber,
                pageSize);
            var draftPostWithUserInfo = await postsCollection
                .Aggregate()
                .Match(_ => _.ProfileId == profileId && _.IsDraft && _.UserId == user.Id)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(_ => _.UserInfo)
                .FirstOrDefaultAsync();
            var draftPostDto = _mapper.Map<PostDto>(draftPostWithUserInfo);
            if (draftPostDto != null)
            {
                draftPostDto.UserInfo.IsCohealer = IsProfileCohealer(user, profile);
            }
            var postDtos = _mapper.Map<ICollection<PostDto>>(data);
            var postDtosWithSubItems = new List<PostDto>();
            foreach (var postDto in postDtos)
            {
                var post = await FillSubItemsForPost(postDto);
                var postUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.Id == postDto.UserId);
                postDto.UserInfo = _mapper.Map<CommunityPostUserDto>(postUser);
                postDto.UserInfo.IsCohealer = IsProfileCohealer(user, profile);
                postDtosWithSubItems.Add(post);
            }
            return new PagedPostDto
            {
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                Posts = postDtosWithSubItems,
                UserDraftPost = await FillSubItemsForPost(draftPostDto)
            };
        }
        public async Task<PostDto> GetByIdAsync(string postId)
        {
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(_ => _.Id == postId)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(_ => _.UserInfo)
                .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                    post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
                .Unwind<Post, Post>(_ => _.Contribution)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(postId));
            }

            var postDto = _mapper.Map<PostDto>(entity);
            postDto.UserInfo.IsCohealer = IsCohealer(entity.UserInfo, entity.Contribution);

            return await FillSubItemsForPost(postDto);
        }

        public async Task<PostDto> GetProfilePostByIdAsync(string postId)
        {
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
               .Aggregate()
               .Match(_ => _.Id == postId)
               .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                   post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
               .Unwind<Post, Post>(_ => _.UserInfo)
               .Lookup<Post, ProfilePage, Post>(_unitOfWork.GetGenericRepositoryAsync<ProfilePage>().Collection,
                   post => post.ProfileId, profile => profile.UserId, result => result.ProfilePage)
               .Unwind<Post, Post>(_ => _.ProfilePage)
               .FirstOrDefaultAsync();
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(postId));
            }
            var postDto = _mapper.Map<PostDto>(entity);
            postDto.UserInfo.IsCohealer = IsProfileCohealer(entity.UserInfo, entity.ProfilePage);
            return await FillSubItemsForPost(postDto);
        }
        public async Task<PostDto> UpdateAsync(UpdatePostRequest request, string currentAccountId)
        {
            var now = DateTime.UtcNow;
            var currentUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == currentAccountId);
            if (currentUser == null)
            {
                throw new ValidationException($"User with {currentAccountId} not found", request);
            }

            var createdPostUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.Id == request.UserId);
            if (!string.IsNullOrEmpty(request.UserId) && createdPostUser == null)
            {
                throw new ValidationException($"User with {request.UserId} not found", request);
            }

          
            if (!string.IsNullOrEmpty(request.ContributionId))
            {
                var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(_ => _.Id == request.ContributionId);
                if (contribution == null)
                {
                    throw new ValidationException($"Contribution {request.ContributionId} not found");
                }
                if (!string.IsNullOrEmpty(request.UserId) && currentUser.Id != request.UserId && !IsCohealer(currentUser, contribution))
                {
                    throw new AccessDeniedException($"User with {currentAccountId} can't edit posts of others");
                }
            }
            else
            {
                var profile = await _unitOfWork.GetGenericRepositoryAsync<ProfilePage>().GetOne(a => a.UserId == request.ProfileId);
                if (profile == null)
                {
                    throw new ValidationException($"Profile with {request.ProfileId} not found", request);
                }
                if (!string.IsNullOrEmpty(request.UserId) && currentUser.Id != request.UserId && !IsProfileCohealer(currentUser, profile))
                {
                    throw new AccessDeniedException($"User with {currentAccountId} can't edit posts of others");
                }
            }


            var entity = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(p => p.Id == request.Id);
            if (entity == null)
            {
                throw new ValidationException($"Post with {request.Id} not found");
            }

            var pinnedTime = UpdatePinningTime(entity, request, now);

            // needed for updating createTime when user submit post, so post will become not draft
            var isCreatedRealPost = !request.IsDraft && entity.IsDraft;

            entity = _mapper.Map(request, entity);

            // TODO: fix with mappings
            entity.UserId = string.IsNullOrEmpty(request.UserId) ? currentUser.Id : request.UserId;
            entity.CreateTime = isCreatedRealPost ? now : entity.CreateTime;
            entity.PinnedTime = pinnedTime;

            var updatedPost = await _unitOfWork.GetRepositoryAsync<Post>().Update(entity.Id, entity, true);

            var postDto = _mapper.Map<PostDto>(await GetByIdAsync(updatedPost.Id));

            return await FillSubItemsForPost(postDto);
        }

        // TODO: deleting sub items
        public async Task<PostDto> DeleteAsync(string postId)
        {
            var entity = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(p => p.Id == postId);
            if (entity == null)
            {
                throw new ValidationException($"Post with {postId} not found");
            }

            await _unitOfWork.GetRepositoryAsync<Post>().Delete(entity);
            try
            {
                var notificationsList = await _unitOfWork.GetRepositoryAsync<FcmNotification>().Get(x => x.NotificationInfo.ContainsKey("PostId"));
                foreach (var notification in notificationsList)
                {
                    if (notification.NotificationInfo.Any(x => x.Value == postId))
                    {
                        await _unitOfWork.GetRepositoryAsync<FcmNotification>().Delete(notification);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return _mapper.Map<PostDto>(entity);
        }

        public async Task<PostDto> AddAttachmentAsync(AddPostAttachmentRequest request, string accountId, bool isProfilepage = false)
        {
            if (request.File.Length < 0)
            {
                throw new ValidationException($"File {request.File} is empty");
            }

            var postDto = new PostDto();
            if (isProfilepage)
            {
                postDto = await GetProfilePostByIdAsync(request.PostId);
            }
            else
            {
                postDto = await GetByIdAsync(request.PostId);
            }
            if (postDto == null)
            {
                throw new ValidationException($"Post with {request.PostId} not found", request);
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(_ => _.AccountId == accountId);
            if (user == null)
            {
                throw new ValidationException($"User with {accountId} not found", request);
            }

            var attachmentId = $"{Guid.NewGuid().ToString()}{Path.GetExtension(request.FileName)}";

            var contentType = request.File.ContentType;
            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", accountId)
                .Replace("{attachmentIdWithExtension}", attachmentId);

            var fileAdditionResult = await _fileStorageManager.UploadFileToStorageAsync(
                request.File.OpenReadStream(), _s3SettingsOptions.PublicBucketName, fileKey, contentType);

            if (fileAdditionResult.Failed)
            {
                throw new FileLoadException(fileAdditionResult.Message);
            }

            var communityAttachments = new List<CommunityAttachment>
            {
                new CommunityAttachment
                {
                    Id = attachmentId,
                    S3Link = fileAdditionResult.Payload.ToString(),
                    Type = contentType,
                    FileName = request.FileName
                }
            };

            if (postDto.Attachments != null && postDto.Attachments.Any())
            {
                communityAttachments.AddRange(postDto.Attachments);
            }

            postDto.Attachments = communityAttachments;

            var updatedPost = await UpdateAsync(_mapper.Map<UpdatePostRequest>(postDto), accountId);

            return updatedPost;
        }

        public async Task<OperationResult> UploadAttachmentForPostAsync(FileDetails fileDetails, string postId, int partNumber, bool isLastPart, string documentId, bool isCommenttype)
        {
            
            PostDto postDto = null;
            if (isCommenttype == false)
            {
                postDto = await GetByIdAsync(postId);
                if (postDto == null)
                {
                    throw new ValidationException($"Post with {postId} not found");
                }
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.AccountId == fileDetails.AccountId);
            if (user == null)
            {
                throw new ValidationException($"User with {fileDetails.AccountId} not found");
            }
            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", fileDetails.AccountId)
                .Replace("{attachmentIdWithExtension}", $"{documentId}{fileDetails.Extension}");

            var fileUploadResult = await _fileStorageManager.UploadObjectAsync(
                fileDetails.FileStream,
                _s3SettingsOptions.PublicBucketName,
                fileKey,
                fileDetails.ContentType,
                partNumber,
                isLastPart,
                fileDetails.UploadId,
                fileDetails.PrevETags);

            if (!fileUploadResult.Succeeded)
            {
                return fileUploadResult;
            }
            var returnedFileDetails = (FileDetails)fileUploadResult.Payload;
            if (string.IsNullOrEmpty(fileDetails.UploadId))
            {
                fileDetails.UploadId = returnedFileDetails?.UploadId;
            }
            fileDetails.PrevETags = returnedFileDetails?.PrevETags;
            if (isLastPart)
            {

                var communityAttachments = new List<CommunityAttachment>
                {
                    new CommunityAttachment
                    {
                        Id = $"{documentId}{fileDetails.Extension}",
                        S3Link = $"https://{_s3SettingsOptions.PublicBucketName}.s3.amazonaws.com/{fileKey}",
                        Type = fileDetails.FileType,
                        FileName = fileDetails.OriginalNameWithExtension
                    }
                };
                if (isCommenttype == false)
                {
                    PostDto updatedPost = null;
                    if (postDto.Attachments != null && postDto.Attachments.Any())
                    {
                        communityAttachments.AddRange(postDto.Attachments);
                    }
                    postDto.Attachments = communityAttachments;
                    updatedPost = await UpdateAsync(_mapper.Map<UpdatePostRequest>(postDto), fileDetails.AccountId);
                    if (updatedPost == null)
                    {
                        await _fileStorageManager.DeleteFileFromNonPublicStorageAsync(fileKey);
                    }
                    return OperationResult.Success("", updatedPost);
                }
                return OperationResult.Success("", communityAttachments);
            }
            fileDetails.FileStream = null;
            return OperationResult.Success("Upload part successfully", fileDetails);
        }
        //TODO: move deletion of file inside update method
        public async Task<PostDto> DeleteAttachmentAsync(string postId, string attachmentId, string accountId, bool isProfilePage = false)
        {
            var postDto = new PostDto();
            if (isProfilePage)
            {
                postDto = await GetProfilePostByIdAsync(postId);
            }
            else
            {
                postDto = await GetByIdAsync(postId);
            }
            if (postDto == null)
            {
                throw new ValidationException($"Post with {postId} not found");
            }

            if (postDto.Attachments.FirstOrDefault(_ => _.Id == attachmentId) == null)
            {
                throw new ValidationException($"Attachment {attachmentId} for post {postId} not found");
            }

            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", accountId)
                .Replace("{attachmentIdWithExtension}", attachmentId);
            var s3Link = $"https://{_s3SettingsOptions.PublicBucketName}.s3.amazonaws.com/{fileKey}";
            var fileDeletionResult = await _fileStorageManager.DeleteFileFromPublicStorageByUrlAsync(s3Link);
            if (fileDeletionResult.Failed)
            {
                throw new FileLoadException(fileDeletionResult.Message);
            }

            postDto.Attachments = postDto.Attachments.Where(_ => _.S3Link != s3Link).ToList();

            var updatedPost = await UpdateAsync(_mapper.Map<UpdatePostRequest>(postDto), accountId);

            return updatedPost;
        }
        


        public async Task<PostDto> MakethePostStarred(string postId, string accountId, bool isStared, bool isProfilePage = false)
        {
            var postModel = new PostDto();
            if (isProfilePage)
            {
                postModel = await GetProfilePostByIdAsync(postId);
            }
            else
            {
                postModel = await GetByIdAsync(postId);
            }
            await GetByIdAsync(postId);
            if (postModel == null)
            {
                throw new ValidationException($"Post with {postId} not found");
            }
            postModel.IsStarred = isStared;
            var updatedPost = await UpdateAsync(_mapper.Map<UpdatePostRequest>(postModel), accountId);
            return updatedPost;
        }
        public async Task<List<PostDto>> GetAllStarredPost(string userId, bool isProfilePage = false)
        {
            var postList = new List<PostDto>();
            if (isProfilePage)
            {
                postList = await GetProfilePostsByUserId(userId);
            }
            else
            {
                postList = await GetPostsByUserId(userId);
            }
            return postList;
        }
        private async Task<List<PostDto>> GetPostsByUserId(string userId)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.UserId == userId && m.IsStarred == true)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                    post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
                .Unwind<Post, Post>(_ => _.Contribution)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }

        private async Task<List<PostDto>> GetProfilePostsByUserId(string userId)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.UserId == userId && m.IsStarred == true)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ProfilePage, Post>(_unitOfWork.GetGenericRepositoryAsync<ProfilePage>().Collection,
                    post => post.ProfileId, profilePage => profilePage.Id, result => result.ProfilePage)
                .Unwind<Post, Post>(_ => _.ProfilePage)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        public async Task<List<PostDto>> GetPostsUsingKeywordsSearch(string contributionId, string keywords)
        {
            List<PostDto> postList = new List<PostDto>();
            var filteredKeywords = string.Empty;
            if (keywords.Contains("#"))
            {
                // Search Only hashtags.
                var hashtagsCount = keywords.Count(f => (f == '#'));
                if (keywords.StartsWith("#") && !keywords.Contains(" ") && hashtagsCount <= 1)
                {
                     filteredKeywords = keywords;
                     postList = await GetPostsByKewordsUsingHashTags(contributionId, filteredKeywords);
                    return postList;
                }
                else
                {
                    filteredKeywords = keywords.Replace("#", "");
                    postList = await GetPostsByKeywordsUsingText(contributionId, filteredKeywords);
                    return postList;
                }
            }
            else
            {
                filteredKeywords = keywords;
                postList = await GetPostsByKeywordsUsingText(contributionId, filteredKeywords);
                return postList;
            }
            
        }
        public async Task<List<PostDto>> GetProfilePostsUsingKeywordsSearch(string profileId, string keywords)
        {
            List<PostDto> postList = new List<PostDto>();
            var filteredKeywords = string.Empty;
            if (keywords.Contains("#"))
            {
                // Search Only hashtags.
                var hashtagsCount = keywords.Count(f => (f == '#'));
                if (keywords.StartsWith("#") && !keywords.Contains(" ") && hashtagsCount <= 1)
                {
                    filteredKeywords = keywords;
                    postList = await GetProfilePostsUsingHashTags(profileId, filteredKeywords);
                    return postList;
                }
                else
                {
                    filteredKeywords = keywords.Replace("#", "");
                    postList = await GetProfilePostsByKeyWordUsingText(profileId, filteredKeywords);
                    return postList;
                }
            }
            else
            {
                filteredKeywords = keywords;
                postList = await GetProfilePostsByKeyWordUsingText(profileId, filteredKeywords);
                return postList;
            }
        }
        public async Task<OperationResult> SaveLastSeenForPosts(string accountId, string contributionId, bool isRead)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("Unable to find User.");
            }
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            if (contribution == null)
            {
                return OperationResult.Failure("Unable to find Contribution.");
            }
            if (isRead)
            {
                user.PostLastSeen[contributionId] = System.DateTime.UtcNow;
            }
            else
            {
                if (!user.PostLastSeen.ContainsKey(contributionId))
                {
                    return OperationResult.Failure("Unable to find post to mark as Unread.");
                }
                user.PostLastSeen.Remove(contributionId);
            }
            await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            return OperationResult.Success();
        }
        public async Task<OperationResult> SaveLastSeenForProfilePosts(string accountId, string profileId, bool isRead)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("Unable to find User.");
            }
            var contribution = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(x => x.UserId == profileId);
            if (contribution == null)
            {
                return OperationResult.Failure("Unable to find Contribution.");
            }
            if (isRead)
            {
                user.PostLastSeen[profileId] = System.DateTime.UtcNow;
            }
            else
            {
                if (!user.PostLastSeen.ContainsKey(profileId))
                {
                    return OperationResult.Failure("Unable to find post to mark as Unread.");
                }
                user.PostLastSeen.Remove(profileId);
            }
            await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            return OperationResult.Success();
        }
        public async Task<OperationResult> GetLastSeenPostsCount(string accountId, string contributionId)
        {
            var result = new Dictionary<string, long>();
            long postsCount = 0;
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
            if (user.PostLastSeen.ContainsKey(contributionId))
            {
                postsCount = await postsRepository.Count(x =>
                    x.ContributionId == contributionId && !x.IsDraft && x.CreateTime > user.PostLastSeen[contributionId]);
                result.Add(contributionId, postsCount);
            }
            else
            {
                postsCount = await postsRepository.Count(x =>
                   x.ContributionId == contributionId && !x.IsDraft);
                result.Add(contributionId, postsCount);
            }
            return OperationResult.Success(null, result);
        }
        public async Task<OperationResult> GetLastSeenProfilePostsCount(string accountId, string profileId)
        {
            var result = new Dictionary<string, long>();
            long postsCount = 0;
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
            if (user.PostLastSeen.ContainsKey(profileId))
            {
                postsCount = await postsRepository.Count(x =>
                    x.ProfileId == profileId && !x.IsDraft && x.CreateTime > user.PostLastSeen[profileId]);
                result.Add(profileId, postsCount);
            }
            else
            {
                postsCount = await postsRepository.Count(x =>
                   x.ProfileId == profileId && !x.IsDraft);
                result.Add(profileId, postsCount);
            }
            return OperationResult.Success(null, result);
        }
        private async Task<List<PostDto>> GetPostsByKeywordsUsingText(string contributionId, string keywords)
        {
            
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.ContributionId == contributionId && m.Text.ToLower().Contains(keywords.ToLower()))
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                    post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
                .Unwind<Post, Post>(c => c.Contribution)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        private async Task<List<PostDto>> GetProfilePostsByKeyWordUsingText(string profileId, string keywords)
        {
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                 .Aggregate()
                 .Match(m => m.ProfileId == profileId && m.Text.ToLower().Contains(keywords.ToLower()))
                 .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                     post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                 .Unwind<Post, Post>(l => l.UserInfo)
                 .Lookup<Post, ProfilePage, Post>(_unitOfWork.GetGenericRepositoryAsync<ProfilePage>().Collection,
                     post => post.ProfileId, profilePage => profilePage.Id, result => result.ProfilePage)
                 .Unwind<Post, Post>(c => c.ProfilePage)
                 .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        private async Task<List<PostDto>> GetPostsByKewordsUsingHashTags(string contributionId, string keywords)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.ContributionId == contributionId && m.HashTags.Contains(keywords))
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                    post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
                .Unwind<Post, Post>(c => c.Contribution)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        private async Task<List<PostDto>> GetProfilePostsUsingHashTags(string profileId, string keywords)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.ProfileId == profileId && m.HashTags.Contains(keywords))
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ProfilePage, Post>(_unitOfWork.GetGenericRepositoryAsync<ProfilePage>().Collection,
                    post => post.ProfileId, contribution => contribution.Id, result => result.ProfilePage)
                .Unwind<Post, Post>(c => c.ProfilePage)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        public async Task<List<PostDto>> GetAllUserTaggedPosts(string userId, string contributionId)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.TaggedUserIds.Contains(userId) && m.ContributionId == contributionId)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                    post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
                .Unwind<Post, Post>(_ => _.Contribution)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }
        public async Task<List<PostDto>> GetAllPinedPostsInContribution(string contributionId, bool isPinned, int? skip, int? take)
        {
            List<PostDto> postList = new List<PostDto>();
            List<Post> entity = new List<Post>();
            if (isPinned)
            {
               entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
              .Aggregate()
              .Match(m => m.ContributionId == contributionId && m.IsPinned == true)
              .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                  post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
              .Unwind<Post, Post>(l => l.UserInfo)
              .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                  post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
              .Unwind<Post, Post>(_ => _.Contribution)
              .ToListAsync();
            }
            else
            {
                entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
               .Aggregate()
               .Match(m => m.ContributionId == contributionId)
               .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                   post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
               .Unwind<Post, Post>(l => l.UserInfo)
               .Lookup<Post, ContributionBase, Post>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection,
                   post => post.ContributionId, contribution => contribution.Id, result => result.Contribution)
               .Unwind<Post, Post>(_ => _.Contribution)
               .ToListAsync();
            }
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return skip != null && take != null ? postDto.Skip(Convert.ToInt32(skip)).Take(Convert.ToInt32(take)).ToList() : postDto;

        }
        public async Task<List<PostDto>> GetAllUserTaggedPostsByProfile(string userId, string profileId)
        {
            List<PostDto> postList = new List<PostDto>();
            var entity = await _unitOfWork.GetGenericRepositoryAsync<Post>().Collection
                .Aggregate()
                .Match(m => m.TaggedUserIds.Contains(userId) && m.ProfileId == profileId)
                .Lookup<Post, User, Post>(_unitOfWork.GetGenericRepositoryAsync<User>().Collection,
                    post => post.UserId, userInfo => userInfo.Id, result => result.UserInfo)
                .Unwind<Post, Post>(l => l.UserInfo)
                .Lookup<Post, ProfilePage, Post>(_unitOfWork.GetGenericRepositoryAsync<ProfilePage>().Collection,
                    post => post.ProfileId, profilePage => profilePage.Id, result => result.ProfilePage)
                .Unwind<Post, Post>(_ => _.ProfilePage)
                .ToListAsync();
            var postDto = _mapper.Map<List<PostDto>>(entity);
            return postDto;
        }

        // TODO: refactoring for retrieving data in one request
        private async Task<PostDto> FillSubItemsForPost(PostDto postDto)
        {
            if (postDto == null)
            {
                return null;
            }

            var comments = await _commentService.GetAllForPostAsync(postDto.Id);
            var likes = await _likeService.GetAllForPostAsync(postDto.Id);
            var links = await GetCachedLinks(postDto);

            postDto.Comments = comments;
            postDto.Likes = likes;
            postDto.Links = links;

            return postDto;
        }

        private async Task<IEnumerable<Link>> GetCachedLinks(PostDto postDto)
        {
            const string urlRegex = "(http|ftp|https)://([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:/~+#-]*[\\w@?^=%&/~+#-])?";
            var links = new List<Link>();

            if (string.IsNullOrEmpty(postDto.Text))
            {
                return links;
            }
            var matches = Regex.Matches(postDto.Text, urlRegex);

            foreach (var link in matches)
            {
                var value = await _memoryCache.GetOrCreateAsync(link.ToString(), async entry =>
                {
                    entry.SetSlidingExpiration(TimeSpan.FromHours(2));
                    return await GetLinkForPreview(link.ToString(), postDto.Id);
                });

                links.Add(value);
            }

            return links;
        }

        private static async Task<Link> GetLinkForPreview(string link, string postId)
        {
            Link previewObject = new Link
            {
                PostId = postId,
                ImageUrl = string.Empty,
                Title = String.Empty,
                Description = String.Empty,
                Url = link
            };
            try
            {
                var httpClient = new HttpClient();
                var parser = new HtmlParser();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");
                httpClient.Timeout = TimeSpan.FromSeconds(2);
                var request = await httpClient.GetAsync(link);
                var response = await request.Content.ReadAsStreamAsync();

                var document = parser.ParseDocument(response);

                var url = (document.Head.SelectSingleNode("//meta[@property=\"og:url\"]") as IHtmlMetaElement)?.Content;

                var title = (document.Head.SelectSingleNode("//meta[@property=\"og:title\"]") as IHtmlMetaElement)?.Content;

                var description =
                        (document.Head.SelectSingleNode("//meta[@property=\"og:description\"]") as IHtmlMetaElement)?.Content;

                var image = (document.Head.SelectSingleNode("//meta[@name=\"og:image\" or @property=\"og:image\"]") as IHtmlMetaElement)?.Content;

                previewObject.ImageUrl = image;
                previewObject.Title = title;
                previewObject.Description = description;
                    
            }
            catch
            {
            }

            return previewObject;
        }

        // TODO: refactor that thing
        private static DateTime UpdatePinningTime(Post post, UpdatePostRequest request, DateTime now)
        {
            var isPostBecamePinned = request.IsPinned && !post.IsPinned;
            var isPostBecameUnpinned = !request.IsPinned && post.IsPinned;

            if (isPostBecamePinned)
            {
                return now;
            }

            return isPostBecameUnpinned ? default : post.PinnedTime;
        }

        private static bool IsCohealer(BaseEntity user, ContributionBase contributionBase)
        {
            return user.Id == contributionBase.UserId ||
                   contributionBase.Partners.Any(x => x.IsAssigned && x.UserId == user.Id);
        }
        private static bool IsProfileCohealer(BaseEntity user, ProfilePage ProfilePage)
        {
            return user.Id == ProfilePage.UserId;
        }
        private async Task<bool> IsUserHasAccessToPrivatePosts(User user, ContributionBase contributionBase)
        {
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(_ => _.ClientId == user.Id && _.ContributionId == contributionBase.Id);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contributionBase);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
            var isUserAdmin = await _accountManager.IsAdminOrSuperAdmin(user.AccountId);
            var isCohealer = IsCohealer(user, contributionBase);
            return purchaseVms.Any(p => p.HasSucceededPayment) || isUserAdmin || isCohealer;
        }

        private async Task<FilterDefinition<Post>> GetFilterForPosts(User user, ContributionBase contributionBase)
        {
            var isUserHasAccessToPrivatePosts = await IsUserHasAccessToPrivatePosts(user, contributionBase);
            var baseFilter = Builders<Post>.Filter.Where(_ => _.ContributionId == contributionBase.Id && !_.IsDraft);

            var filterExcludePrivatePostsOfOtherUsers = Builders<Post>.Filter.Where(_ =>
                _.ContributionId == contributionBase.Id && !_.IsDraft && (!_.IsPrivate || (_.IsPrivate && _.UserId == user.Id)));
            var filterExcludePrivatePosts =
                Builders<Post>.Filter.Where(_ => _.ContributionId == contributionBase.Id && !_.IsDraft && !_.IsPrivate);
            var privatePostFilter =
                IsCohealer(user, contributionBase) ? filterExcludePrivatePostsOfOtherUsers : filterExcludePrivatePosts;

            return isUserHasAccessToPrivatePosts ? baseFilter : privatePostFilter;
        }

        private FilterDefinition<Post> GetFilterForProfilePosts(User user, string profileId)
        {

            var baseFilter = Builders<Post>.Filter.Where(_ => _.ProfileId == profileId && !_.IsDraft);
            return baseFilter;
        }
        public async Task<OperationResult> SaveHashtag(string hashtagText)
        {
            var filteredText =  Regex.Replace(hashtagText, @"\s+", String.Empty);
            var newHashtag = new CommunityHashtags { Text = filteredText };
            var exsitedHashTag = await _unitOfWork.GetRepositoryAsync<CommunityHashtags>().GetOne(m => m.Text == filteredText);
            {
                if(exsitedHashTag == null)
                {
                    var hashtags = await _unitOfWork.GetRepositoryAsync<CommunityHashtags>().Insert(newHashtag);
                    return OperationResult.Success("Hashtag saved Successfully", hashtags);
                }
            }
            return OperationResult.Failure("HashTag Already Exists.");
       }
        public async Task<List<string>> GetAllCommunityHashtags(string searchText)
        {
            List<string> list = new List<string>();
            var filteredText = Regex.Replace(searchText, @"\s+", String.Empty);
            var hashtags = await _unitOfWork.GetRepositoryAsync<CommunityHashtags>().GetSkipTakeWithSort(m => m.Text.StartsWith(filteredText), 0 , 30,OrderByEnum.Asc);
            list = _mapper.Map<List<CommunityHashtags>>(hashtags).Select(h => h.Text).ToList();
            return list;
        }
        
    }
}