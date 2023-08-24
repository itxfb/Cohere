using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Api.Models.Responses;
using Cohere.Api.Utils;
using Cohere.Domain.Models.Community.Attachment.Request;
using Cohere.Domain.Models.Community.Post;
using Cohere.Domain.Models.Community.Post.Request;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.Abstractions.Community;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers.Community
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Roles = "Cohealer, Client")]
    public class PostController : CohereController
    {
        private readonly IPostService _postService;
        private readonly IJobScheduler _jobScheduler;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFCMService _fcmService;

        public PostController(IPostService postService, IJobScheduler jobScheduler, IMapper mapper, IUnitOfWork unitOfWork, IFCMService fcmService)
        {
            _postService = postService;
            _jobScheduler = jobScheduler;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _fcmService = fcmService;
        }

        /// <summary>
        /// Create post
        /// </summary>
        /// <param name="request">Model as a <see cref="CreatePostRequest" />.</param>
        /// <response code="200">Creation Post successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Created Post </returns>
        /// <example>POST: api/Post </example>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
        {
            var createdPostDto = await _postService.AddAsync(request, AccountId);
            if (createdPostDto.IsScheduled == true && createdPostDto.IsDraft != true)
            {
                DateTime scheduletime = Convert.ToDateTime(createdPostDto.ScheduledTime);
                DateTime now = DateTime.UtcNow;
                if (now < scheduletime)
                {
                    TimeSpan dff = (scheduletime.Subtract(now));
                    string JobId = _jobScheduler.ScheduleJob<ISchedulePostJob>(dff, createdPostDto.Id);
                    Post post = _unitOfWork.GetRepositoryAsync<Post>().GetOne(x => x.Id == createdPostDto.Id).Result;
                    post.ScheduledJobId = JobId;
                    var updatedPost = _unitOfWork.GetRepositoryAsync<Post>().Update(post.Id, post, true);
                }
            }
            return Ok(createdPostDto);
        }

        /// <summary>
        /// Returns all posts for contribution paged
        /// </summary>
        /// <response code="200">Posts returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Paged list of posts for contribution</returns>
        /// <example>GET: Post?/GetAll/{contributionId}/{pageNumber:int}/{pageSize:int}</example>
        [HttpGet("GetAll/{contributionId}/{pageNumber:int}/{pageSize:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedPostDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetAllForContribution([FromRoute] string contributionId, int pageNumber, int pageSize, bool skipPinnedPosts = false)
        {
            var pagedPostDto = await _postService.GetAllForContributionAsync(contributionId, AccountId, pageNumber, pageSize, skipPinnedPosts);
            return Ok(pagedPostDto);
        }


        /// <summary>
        /// Returns all posts for profile paged
        /// </summary>
        /// <response code="200">Posts returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Paged list of posts for contribution</returns>
        /// <example>GET: Post?/GetAll/{contributionId}/{pageNumber:int}/{pageSize:int}</example>
        [HttpGet("GetAllProfilePosts/{profileId}/{pageNumber:int}/{pageSize:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedPostDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetAllForProfile([FromRoute] string profileId, int pageNumber, int pageSize)
        {
            var pagedPostDto = await _postService.GetAllForProfileAsync(profileId, AccountId, pageNumber, pageSize);
            return Ok(pagedPostDto);
        }

        /// <summary>
        /// Returns post by id
        /// </summary>
        /// <response code="200">Post returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Post by id</returns>
        /// <example>GET: Post?/{postId}</example>
        [HttpGet("{postId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetById([FromRoute] string postId)
        {
            var communityPost = await _postService.GetByIdAsync(postId);
            return Ok(communityPost);
        }


        /// <summary>
        /// Returns Profile post by id
        /// </summary>
        /// <response code="200">Post returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Post by id</returns>
        /// <example>GET: Post?/{postId}</example>
        [HttpGet("GetProfilePost/{profilePostId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetProfilePostById([FromRoute] string profilePostId)
        {
            var communityPost = await _postService.GetProfilePostByIdAsync(profilePostId);
            return Ok(communityPost);
        }

        /// <summary>
        /// Update Post
        /// </summary>
        /// <param name="request">Model as a <see cref="UpdatePostRequest" />.</param>
        /// <response code="200">Edition Post successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Updated Post </returns>
        /// <example>PUT: api/Post </example>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Update([FromBody] UpdatePostRequest request)
        {
            var existedPost = new PostDto();
            if (!string.IsNullOrEmpty(request.ContributionId))
            {
                existedPost = await _postService.GetByIdAsync(request.Id);
            }
            else
            {
                existedPost = await _postService.GetProfilePostByIdAsync(request.Id);
            }
            var updatedPost = await _postService.UpdateAsync(request, AccountId);
            Post post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(x => x.Id == updatedPost.Id);
            if (updatedPost.IsScheduled == true && updatedPost.IsDraft != true)
            {
                DateTime scheduletime = Convert.ToDateTime(updatedPost.ScheduledTime);
                DateTime now = DateTime.UtcNow;
                if (now < scheduletime)
                {
                    TimeSpan dff = (scheduletime.Subtract(now));
                    string JobId = "";
                    if (!string.IsNullOrEmpty(updatedPost.ScheduledJobId))
                    {
                        JobId = _jobScheduler.UpdateScheduleJob<ISchedulePostJob>(updatedPost.ScheduledJobId, dff, updatedPost.Id);
                    }
                    else
                    {
                        JobId = _jobScheduler.ScheduleJob<ISchedulePostJob>(dff, updatedPost.Id);
                    }
                    post.ScheduledJobId = JobId;
                    var newupdatedPost = _unitOfWork.GetRepositoryAsync<Post>().Update(post.Id, post, true);
                }
            }else if(existedPost.IsDraft && !updatedPost.IsDraft)
            {
                var IsfirstPost = await _unitOfWork.GetRepositoryAsync<Post>().Get(p => p.UserId == updatedPost.UserId && p.ContributionId==updatedPost.ContributionId);
                if (IsfirstPost != null && IsfirstPost.Where(x=>!x.IsDraft).Count() == 1)
                {
                    try
                    {
                        await _fcmService.SendPostPushNotification(post);
                    }
                    catch
                    {

                    }
                }
                
            }
            if(!existedPost.IsPinned && updatedPost.IsPinned)
            {
                try
                {
                    await _fcmService.SendPinnedPostPushNotification(post,AccountId);
                }
                catch
                {

                }
            }
            return Ok(updatedPost);
        }

        /// <summary>
        /// Removes post by Id
        /// </summary>
        /// <param name="postId">Post id</param>
        /// <response code="200">Post removed successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Deleted Post </returns>
        /// <example>DELETE: api/Post/{postId}</example>
        [HttpDelete("{postId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Delete([FromRoute] string postId)
        {
            var deletedPostId = await _postService.DeleteAsync(postId);
            return Ok(deletedPostId);
        }

        /// <summary>
        /// Add attachment for post
        /// </summary>
        /// <param name="request">Model as a <see cref="AddPostAttachmentRequest" />.</param>
        /// <response code="200">Addition of attachment for post successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Added attachment </returns>
        /// <example>POST: api/Post/Attachment </example>
        [HttpPost("Attachment")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PostDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> AddAttachment([FromForm] AddPostAttachmentRequest request, [FromForm] bool isProfilePage = false)
        {
            var createdPostAttachmentDto = await _postService.AddAttachmentAsync(request, AccountId, isProfilePage);
            return Ok(createdPostAttachmentDto);
        }

        /// <summary>
        /// Delete attachment for post
        /// </summary>
        /// <param name="postId">Post id</param>
        /// <param name="attachmentId">Attachment id</param>
        /// <response code="200">Deletion of attachment for post successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Deleted attachment </returns>
        /// <example>DELETE: api/Post/Attachment/{postId}/{attachmentId} </example>
        [HttpDelete("Attachment/{postId}/{attachmentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> DeleteAttachment([FromRoute] string postId, string attachmentId, bool isProfilePage = false)
        {
            var deletedPostAttachment = await _postService.DeleteAttachmentAsync(postId, attachmentId, AccountId);
            return Ok(deletedPostAttachment);
        }
        [HttpPost("MakethePostStarred")]
        public async Task<IActionResult> MakethePostStarred([FromForm] string postId, [FromForm] bool isStared, [FromForm] bool isProfilePage = false)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(postId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("PostId can not be null or empty."));
            }
            var starredPost = await _postService.MakethePostStarred(postId, AccountId, isStared, isProfilePage);
            return Ok(starredPost);
        }
        [HttpGet("GetAllStarredPost")]
        public async Task<IActionResult> GetAllStarredPost([FromForm] string userId, [FromForm] bool isProfilePage = false)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("UserId can not be null or empty."));
            }
            var starredPost = await _postService.GetAllStarredPost(userId);
            return Ok(starredPost);
        }

        [HttpGet("GetAllUserTaggedPosts")]
        public async Task<IActionResult> GetAllUserTaggedPosts([FromForm] string userId, [FromForm] string contributionId)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("UserId can not be null or empty."));
            }
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            var post = await _postService.GetAllUserTaggedPosts(userId, contributionId);
            return Ok(post);
        }

        [HttpGet("GetAllUserTaggedPostsByProfile")]
        public async Task<IActionResult> GetAllUserTaggedPostsByProfile([FromForm] string userId, [FromForm] string profileId)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("UserId can not be null or empty."));
            }
            if (string.IsNullOrEmpty(profileId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ProfileId can not be null or empty."));
            }
            var post = await _postService.GetAllUserTaggedPostsByProfile(userId, profileId);
            return Ok(post);
        }

        [HttpGet("GetPostsUsingKeywordsSearch")]
        public async Task<IActionResult> GetPostsUsingKeywordsSearch([FromForm] string contributionId, [FromForm] string keywords)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            if (string.IsNullOrEmpty(keywords))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Please enter some keywords."));
            }
            var Post = await _postService.GetPostsUsingKeywordsSearch(contributionId, keywords);
            return Ok(Post);
        }

        [HttpGet("GetProfilePostsUsingKeywordsSearch")]
        public async Task<IActionResult> GetProfilePostsUsingKeywordsSearch([FromForm] string profileId, [FromForm] string keywords)
        {
            if (AccountId == null)
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(profileId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("profileId can not be null or empty."));
            }
            if (string.IsNullOrEmpty(keywords))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Please enter some keywords."));
            }
            var Post = await _postService.GetProfilePostsUsingKeywordsSearch(profileId, keywords);
            return Ok(Post);
        }

        [Authorize]
        [HttpPost("SaveLastSeenForPosts")]
        public async Task<IActionResult> SaveLastSeenForPosts([FromForm] string contributionId, [FromForm] bool isRead = true)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            var result = await _postService.SaveLastSeenForPosts(AccountId, contributionId, isRead);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo { Message = result.Message });
        }

        [Authorize]
        [HttpPost("SaveLastSeenForProfilePosts")]
        public async Task<IActionResult> SaveLastSeenForProfilePosts([FromForm] string profileId, [FromForm] bool isRead = true)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
            if (string.IsNullOrEmpty(profileId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ProfileId can not be null or empty."));
            }
            var result = await _postService.SaveLastSeenForProfilePosts(AccountId, profileId, isRead);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo { Message = result.Message });
        }

        [Authorize]
        [HttpGet("GetLastSeenPostsCount")]
        public async Task<IActionResult> GetLastSeenPostsCount([FromQuery] string contributionId)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }
                  
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            var result = await _postService.GetLastSeenPostsCount(AccountId, contributionId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("GetLastSeenProfilePostsCount")]
        public async Task<IActionResult> GetLastSeenProfilePostsCount([FromQuery] string profileId)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(profileId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            var result = await _postService.GetLastSeenProfilePostsCount(AccountId, profileId);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("saveHashtag")]
        public async Task<IActionResult> SaveHashtag(string hashtagText)
        {
            if (string.IsNullOrEmpty(hashtagText))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Hashtag Text can not be null or empty."));
            }
            if (!hashtagText.StartsWith("#"))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("You can not save this text its not a hashtag."));
            }
            var result = await _postService.SaveHashtag(hashtagText);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }
            return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo { Message = result.Message});
        }
        [Authorize]
        [HttpGet("GetAllCommunityHashtags")]
        public async Task<IActionResult> GetAllCommunityHashtags(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("Search Text should not be null or empty."));
            }
            if (!searchText.StartsWith("#"))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("You can not search this text its not a hashtag."));
            }
            var result = await _postService.GetAllCommunityHashtags(searchText);
            return Ok(result);
        }
        [HttpGet("GetAllPinedPostsInContribution")]
        public async Task<IActionResult> GetAllPinedPostsInContribution([FromQuery] string contributionId, bool isPinned, int? skip, int? take)
        {
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new Domain.Models.ModelsAuxiliary.ErrorInfo("ContributionId can not be null or empty."));
            }
            var post = await _postService.GetAllPinedPostsInContribution(contributionId, isPinned, skip, take);
            return Ok(post);
        }
    }
}