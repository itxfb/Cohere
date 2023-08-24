using System.Threading.Tasks;

using Cohere.Api.Models.Responses;
using Cohere.Api.Utils;
using Cohere.Domain.Models.Community.Like;
using Cohere.Domain.Models.Community.Like.Request;
using Cohere.Domain.Service.Abstractions.Community;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers.Community
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Roles = "Cohealer, Client")]
    public class LikeController : CohereController
    {
        private readonly ILikeService _likeService;

        public LikeController(ILikeService likeService)
        {
            _likeService = likeService;
        }

        /// <summary>
        /// Add like
        /// </summary>
        /// <param name="request">Model as a <see cref="AddLikeRequest" />.</param>
        /// <response code="200">Addition Like successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Added Like </returns>
        /// <example>POST: api/Like </example>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LikeDto))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Add([FromBody] AddLikeRequest request)
        {
            var likeDto = await _likeService.AddAsync(request, AccountId);
            return Ok(likeDto);
        }

        /// <summary>
        /// Removes like by Id
        /// </summary>
        /// <param name="likeId">Like id</param>
        /// <response code="200">Like removed successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns> Deleted Like </returns>
        /// <example>DELETE: api/Like/{likeId}</example>
        [HttpDelete("{likeId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Delete([FromRoute] string likeId)
        {
            var deletedLike = await _likeService.DeleteAsync(likeId);
            return Ok(deletedLike);
        }
    }
}