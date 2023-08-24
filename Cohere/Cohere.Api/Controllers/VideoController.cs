using System.Text.Json;
using System.Threading.Tasks;
using Cohere.Api.Controllers.Models;
using Cohere.Api.Filters;
using Cohere.Api.Utils;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service.Abstractions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class VideoController : CohereController
    {
        private readonly IVideoService _videoService;
        private readonly IValidator<GetVideoTokenViewModel> _getTokenValidator;
        private readonly IValidator<DeleteRoomInfoViewModel> _deleteRoomValidator;
        private readonly IValidator<TwilioVideoWebHookModel> _webHookModelVlidator;
        private readonly IValidator<TwilioCompositionWebHookModel> _compositionWebHookModelValidator;
        private readonly ILogger<VideoController> _logger;

        public VideoController(
            IVideoService videoService,
            IValidator<GetVideoTokenViewModel> getTokenValidator,
            IValidator<DeleteRoomInfoViewModel> deleteRoomValidator,
            IValidator<TwilioVideoWebHookModel> webHookModelVlidator,
            IValidator<TwilioCompositionWebHookModel> compositionWebHookModelValidator,
            ILogger<VideoController> logger)
        {
            _videoService = videoService;
            _getTokenValidator = getTokenValidator;
            _deleteRoomValidator = deleteRoomValidator;
            _webHookModelVlidator = webHookModelVlidator;
            _compositionWebHookModelValidator = compositionWebHookModelValidator;
            _logger = logger;
        }

        // POST: /Video/GetClientToken
        [Authorize]
        [HttpPost("GetClientToken")]
        public async Task<IActionResult> GetClientToken(GetVideoTokenViewModel viewModel)
        {
            if (viewModel == null)
            {
                return BadRequest(new ErrorInfo("Get video token model must not be null"));
            }

            ValidationResult validationResult = await _getTokenValidator.ValidateAsync(viewModel);

            if (validationResult.IsValid)
            {
                OperationResult result = await _videoService.GetClientTokenAsync(viewModel, AccountId);
                if (result.Succeeded)
                {
                    return Ok((GetTokenViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        /// <summary>
        /// Gets real room status for class
        /// </summary>
        /// <param name="contributionId">The contribution identifier.</param>
        /// <param name="classId">The class identifier.</param>
        /// <returns>Retrieve room status</returns>
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpGet("GetRoomStatus")]
        public async Task<IActionResult> GetRoomStatus(string contributionId, string classId)
        {
            if (string.IsNullOrWhiteSpace(contributionId))
            {
                return BadRequest($"{nameof(contributionId)} is null or empty");
            }

            if (string.IsNullOrWhiteSpace(classId))
            {
                return BadRequest($"{nameof(classId)} is null or empty");
            }

            OperationResult result = await _videoService.GetRoomStatus(AccountId, contributionId, classId);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            if (result.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(result.Message);
        }

        // POST: /Video/CreateRoomAndGetToken
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("CreateRoomAndGetToken")]
        public async Task<IActionResult> CreateRoomAndGetToken(GetVideoTokenViewModel viewModel)
        {
            if (viewModel == null)
            {
                return BadRequest(new ErrorInfo("Get video token model must not be null"));
            }

            ValidationResult validationResult = await _getTokenValidator.ValidateAsync(viewModel);

            if (validationResult.IsValid)
            {
                OperationResult result = await _videoService.CreateRoom(viewModel, AccountId);
                if (result.Succeeded)
                {
                    return Ok((CreatedRoomAndGetTokenViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        // POST: /Video/DeleteRoom
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("DeleteRoom")]
        public async Task<IActionResult> DeleteRoom(DeleteRoomInfoViewModel viewModel)
        {
            if (viewModel == null)
            {
                return BadRequest(new ErrorInfo("Delete room model must not be null"));
            }

            ValidationResult validationResult = await _deleteRoomValidator.ValidateAsync(viewModel);

            if (validationResult.IsValid)
            {
                OperationResult result = await _videoService.DeleteRoom(viewModel, AccountId);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        [ServiceFilter(typeof(ValidateTwilioRequestAttribute))]
        [HttpPost("HandleTwilioEvent/{contributionId}")]
        public async Task<IActionResult> HandleTwilioEvent([FromForm] TwilioVideoWebHookModel model, [FromRoute] string contributionId)
        {
            if (Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                return Forbid();
            }

            var validationResult = await _webHookModelVlidator.ValidateAsync(model);

            _logger.Log(LogLevel.Information, $"Twilio event {JsonSerializer.Serialize(model)} related contribution with Id: {contributionId}");

            if (validationResult.IsValid)
            {
                _logger.Log(LogLevel.Information, $"Before StatusCallbackEvent is 'room-ended'. Twilio event: {JsonSerializer.Serialize(model)}");

                if (model.StatusCallbackEvent == "room-ended")
                {
                    _logger.Log(LogLevel.Information, $"StatusCallbackEvent is 'room-ended'. Twilio event: {JsonSerializer.Serialize(model)}");
                    OperationResult result = await _videoService.HandleRoomDeletionVendorConfirmation(contributionId, model.RoomSid);

                    if (result.Succeeded)
                    {
                        return Ok();
                    }

                    return NotFound(result.Message);
                }
            }

            return Ok();
        }

        [ServiceFilter(typeof(ValidateTwilioRequestAttribute))]
        [HttpPost("HandleCompositionHooks")]
        public async Task<IActionResult> HandleCompositionHooks([FromForm] TwilioCompositionWebHookModel model)
        {
            if (Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                return Forbid();
            }

            var validationResult = await _compositionWebHookModelValidator.ValidateAsync(model);

            var roomSid = model.RoomSid;
            var compositionId = model.CompositionSid;

            _logger.Log(LogLevel.Information, $"Twilio event {JsonSerializer.Serialize(model)} related room with Id: {roomSid}");

            if (validationResult.IsValid)
            {
                if (model.StatusCallbackEvent == "composition-available")
                {
                    await _videoService.NotifyVideoRetrievalService(compositionId, roomSid, model.Timestamp);
                }

                /*
                 * Another possible events:
                 * composition-enqueued
                 * composition-hook-failed
                 * composition-started
                 * composition-available
                 * composition-progress
                 * composition-failed - need to add error handler
                */
                return Ok();
            }

            return BadRequest(validationResult);
        }

        [Authorize]
        [HttpPost("GetPresignedUrl")]
        public async Task<IActionResult> GetPresignedUrl([FromBody] PresignedUrlRequestModel model)
        {
            try
            {
                var presignedUrl = await _videoService.GetPresignedUrl(AccountId, model.RoomId, model.ContributionId);

                return Ok(presignedUrl);
            }
            catch (AccessDeniedException)
            {
                return Forbid();
            }
        }

        [Authorize]
        [HttpGet("GetPresignedUrlForRecordedSession")]
        public async Task<IActionResult> GetPresignedUrlForRecordedSession(string contributionId, string sessionId, string sessionTimeId = null)
        {
            try
            {
                var presignedUrl = await _videoService.GetPresignedUrlForRecordedSession(AccountId, contributionId, sessionId, sessionTimeId);

                return Ok(presignedUrl);
            }
            catch (AccessDeniedException)
            {
                return Forbid();
            }
        }

        [Authorize]
        [HttpGet("GetVideoUrl")]
        public string GetVideUrl(string videoKey) 
        {
            return _videoService.GetVideoUrl(videoKey);
        }
    }
}