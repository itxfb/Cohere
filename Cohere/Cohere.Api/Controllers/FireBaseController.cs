using Cohere.Api.Utils;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.FCM;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FireBaseController : CohereController
    {
        private readonly ILogger<ExternalCalendarController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFCMService _fcmService;
        public FireBaseController(ILogger<ExternalCalendarController> logger, IUnitOfWork unitOfWork, IFCMService fcmService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _fcmService = fcmService;
        }

        [HttpPost("RemoveUserDeviceToken")]
        public async Task<IActionResult> RemoveUserDeviceToken(string deviceToken)
        {
            if (string.IsNullOrEmpty(deviceToken))
            {
                var errorMessage = $"{deviceToken} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }

            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.RemoveUserDeviceToken(deviceToken, AccountId);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }
    }
}
