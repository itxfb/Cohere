using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecordingController : CohereController
    {
        private readonly IRecordingService _recordingService;

        public RecordingController(IRecordingService recordingService)
        {
            _recordingService = recordingService;
        }

        [HttpGet("GetCurrentRoomStatusById")]
        public async Task<OperationResult> GetCurrentRoomStatus([FromQuery] RecordingRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(AccountId))
            {
                return OperationResult.Failure("Account Id cannot be null");
            }
            return await _recordingService.GetCurrentRoomStatus(request, AccountId);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("Start")]
        public async Task<IActionResult> StartRecording(RecordingRequestModel request)
        {
            return (await _recordingService.ToggleRecording(request, AccountId, true)).ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("Stop")]
        public async Task<IActionResult> StopRecording(RecordingRequestModel request)
        {
            return (await _recordingService.ToggleRecording(request, AccountId, false)).ToActionResult();
        }
    }
}