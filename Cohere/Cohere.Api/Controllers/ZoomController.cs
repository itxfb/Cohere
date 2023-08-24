using Amazon.SQS;
using Cohere.Api.Models;
using Cohere.Api.Utils;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ZoomController : CohereController
    {
        private readonly IZoomService _zoomService;
        private readonly IContributionService _contributionService;
        private readonly ILogger<ZoomController> _logger;
        private readonly IAmazonSQS _amazonSQS;
        private readonly IOptions<SqsSettings> _sqsOptions;
        private readonly ZoomSettings _zoomOptions;

        public ZoomController(IOptions<ZoomSettings> zoomOptions, IZoomService zoomService, IContributionService contributionService, IAmazonSQS amazonSQS, IOptions<SqsSettings> sqsOptions, ILogger<ZoomController> logger)
        {
            _zoomService = zoomService;
            _contributionService = contributionService;
            _amazonSQS = amazonSQS;
            _sqsOptions = sqsOptions;
            _logger = logger;
            _zoomOptions = zoomOptions?.Value;
        }
        
        [HttpPost("HandleRecordingEvents")]
        public async Task<IActionResult> RecordingHooks([FromBody] ZoomRecordCompletedModel model)
        {
            try
            {
                if (model.@event == "endpoint.url_validation")
                {
                    var encoding = new System.Text.ASCIIEncoding();
                    string secretToken = "hk237nSrSLaYp0EWTK03gA";
                    var sha256 = new System.Security.Cryptography.HMACSHA256();
                    sha256.Key = encoding.GetBytes(secretToken);
                    var hash = sha256.ComputeHash(encoding.GetBytes(model.payload.plainToken));
                    var hashed = ToHex(hash, false);
                    return Ok(
                        new
                        {
                            plainToken = model.payload.plainToken,
                            encryptedToken = hashed
                        }
                    );
                }


                _logger.LogError($"Zoom - pushing message in queue method called for Account Id: {model?.payload?.account_id} and meeting Id: {model?.payload?.@object?.id}");
                var authHeader = Request.Headers.FirstOrDefault(x => x.Key == "Authorization");
                var serializedModel = JsonSerializer.Serialize(model);

                if (authHeader.Value != _zoomOptions.VerificationToken)
                {
                    return Unauthorized();
                }

                if (model.@event == "recording.completed")
                {
                    _logger.LogError($"Zoom - pushing message in queue for Account Id: {model?.payload?.account_id} and meeting Id: {model?.payload?.@object?.id}");
                    await _amazonSQS.SendMessageAsync(_sqsOptions.Value.ZoomVideoCompletedQueueUrl, serializedModel);
                    _logger.LogError($"Zoom - message pushed successfully in queue for Account Id: {model?.payload?.account_id} and meeting Id: {model?.payload?.@object?.id}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
            return Ok();
        }

        [HttpPost("HandleDeauthorizationEvents")]
        public async Task<IActionResult> DeauthorizeUser([FromBody] ZoomDeauthorizeModel model)
        {
            try
            {
                var authHeader = Request.Headers.FirstOrDefault(x => x.Key == "Authorization");
                if (authHeader.Value != _zoomOptions.VerificationToken)
                {
                    return Unauthorized();
                }
                if (model.@event == "app_deauthorized")
                {
                    await _zoomService.DeauthorizeUser(model.payload.user_id);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
            return Ok();
        }

        [Authorize]
        [HttpPost("DisconnectZoom")]
        public async Task<IActionResult> DisconnectZoom()
        {
            await _zoomService.DisconnectZoom(AccountId);
            return Ok();
        }

        [Authorize]
        [HttpPost("CreateZoomRefreshToken")]
        public async Task<IActionResult> ZoomRefreshToken([FromBody] CreateZoomModel model)
        {
            await _zoomService.SaveZoomRefreshToken(model.AuthToken, AccountId, model.RedirectUri);
            return Ok();
        }

        [Authorize]
        [HttpGet("GetPresignedUrlForRecordedSession/{meetingId}/{fileName}")]
        public IActionResult GetPresignedUrlForRecordedSession(long meetingId, string fileName, bool asAttachment = false)
        {
            var presignedUrl = _zoomService.GetPresignedUrlForRecording(meetingId, fileName, asAttachment);

            return Ok(presignedUrl);
        }

        private static string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            return result.ToString();
        }

    }
}
