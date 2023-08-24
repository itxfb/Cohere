using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SharedRecordingController : CohereController
    {
        private readonly ISharedRecordingService _sharedRecordingService;

        public SharedRecordingController(ISharedRecordingService sharedRecordingService)
        {
            _sharedRecordingService = sharedRecordingService;
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("addSharedRecordingInfo")]
        public async Task<IActionResult> AddSharedRecordingInfo(string contributionId, string sessionTimeId)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(sessionTimeId))
            {
                return BadRequest("contributionId or sessionTimeId cannot be null or empty");
            }

            var result = await _sharedRecordingService.InsertInfoToShareRecording(contributionId, sessionTimeId, AccountId);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(result.Message);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPut("changePassCodeStatus")]
        public async Task<IActionResult> ChangePassCodeStatus(string contributionId, string sessionTimeId, bool isPassCodeEnabled)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(sessionTimeId))
            {
                return BadRequest("contributionId or sessionTimeId cannot be null or empty");
            }

            var result = await _sharedRecordingService.ChangePassCodeStatus(contributionId, sessionTimeId, AccountId, isPassCodeEnabled);
            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(result.Message);
        }

        [AllowAnonymous]
        [HttpGet("GetSharedRecordingsInfo")]
        public async Task<IActionResult> GetSharedRecordingsInfo(string contributionId, string sessionTimeId, string passCode = null)
        {
            if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(sessionTimeId))
            {
                return BadRequest("contributionId or sessionTimeId cannot be null or empty");
            }

            var result = await _sharedRecordingService.GetSharedRecordingsInfo(contributionId, sessionTimeId, passCode);
            if (result.Succeeded)
            {
                return Ok((List<RecordingInfo>)result.Payload);
            }

            return BadRequest(result.Message);
        }

        [AllowAnonymous]
        [HttpGet("GetSharedRecordingPresignedURL")]
        public async Task<IActionResult> GetSharedRecordingPresignedUrl(string contributionId, string sessionTimeId, string roomId, string passCode = null)
        {
            if (string.IsNullOrEmpty(contributionId) || string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(sessionTimeId))
            {
                return BadRequest("contributionId or roomId/sessionTimeId cannot be null or empty");
            }

            var result = await _sharedRecordingService.GetSharedRecordingPresignedUrl(contributionId,sessionTimeId, roomId, passCode);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(result.Message);
        }
    }
}
