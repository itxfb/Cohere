using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Api.Models.Responses;
using Cohere.Api.Utils;
using Cohere.Domain.Models.Community.Comment;
using Cohere.Domain.Models.Community.Comment.Request;
using Cohere.Domain.Service.Abstractions.Community;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers.Community
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Roles = "Cohealer, Client")]
    public class CommentController : CohereController
    {
        private readonly ICommentService _commentService;

        public CommentController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        /// <summary>
        /// Create comment
        /// </summary>
        /// <param name="request">Model as a <see cref="CreateCommentRequest" />.</param>
        /// <response code="200">Creation Comment successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Created Comment </returns>
        /// <example>POST: api/Comment </example>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CommentDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Create([FromBody] CreateCommentRequest request)
        {
            var commentDto = await _commentService.AddAsync(request, AccountId);
            return Ok(commentDto);
        }

        /// <summary>
        /// Returns all comments for post
        /// </summary>
        /// <response code="200">Comments returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> List of comments for post</returns>
        /// <example>GET: Comment?/getAll/{postId}</example>
        [HttpGet("getAll/{postId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ICollection<CommentDto>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetAllForPost([FromRoute] string postId)
        {
            var communityPostDtos = await _commentService.GetAllForPostAsync(postId);
            return Ok(communityPostDtos);
        }

        /// <summary>
        /// Update comment
        /// </summary>
        /// <param name="request">Model as a <see cref="UpdateCommentRequest" />.</param>
        /// <response code="200">Edition Comment successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Updated Comment </returns>
        /// <example>Put: api/Comment </example>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CommentDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Update([FromBody] UpdateCommentRequest request)
        {
            var commentDto = await _commentService.UpdateAsync(request, AccountId);
            return Ok(commentDto);
        }

        /// <summary>
        /// Removes comment by Id
        /// </summary>
        /// <param name="commentId">Comment id</param>
        /// <response code="200">Comment removed successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Deleted Comment </returns>
        /// <example>DELETE: api/Comment/{commentId}</example>
        [HttpDelete("{commentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Delete([FromRoute] string commentId)
        {
            var deletedComment = await _commentService.DeleteAsync(commentId);
            return Ok(deletedComment);
        }
    }
}