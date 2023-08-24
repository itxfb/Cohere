using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Nylas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ExternalCalendarController : CohereController
    {
        private readonly NylasService _nylasService;
        private readonly ILogger<ExternalCalendarController> _logger;
        private readonly string _nylasClientSecret;

        public ExternalCalendarController(NylasService nylasService, ILogger<ExternalCalendarController> logger, Func<string, string> settingsResolver)
        {
            _nylasService = nylasService;
            _logger = logger;
            _nylasClientSecret = settingsResolver.Invoke(NylasService.ClientSecret);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("AddExternalCalendarAccount")]
        [ProducesResponseType(200, Type = typeof(string))]
        public IActionResult AddAccount(string contributionId, bool isCreated)
        {
            try
            {
                var result = _nylasService.GetAuthorisationUrl(contributionId, isCreated);
                if (result.Succeeded)
                {
                    return Ok((string)result.Payload);
                }
                else
                {
                    _logger.LogError(result.Message);
                    return BadRequest(result.Message);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("ExternalCalendarSignInCallback")]
        public async Task<IActionResult> AddAccountAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                var errorMessage = $"{code} should not be null or empty";
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

                var result = await _nylasService.AddAccountAsync(AccountId, code);

                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpDelete("RemoveExternalCalendarAccount")]
        public async Task<IActionResult> RemoveAccountAsync(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                var errorMessage = $"{emailAddress} should not be null or empty";
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

                var result = await _nylasService.RemoveNylasAccountAsync(AccountId, emailAddress);

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


        [Authorize(Roles = "Cohealer")]
        [HttpPut("SetExternalCalendarAsDefault")]
        public async Task<IActionResult> DefaultAccountAsync(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                var errorMessage = $"{emailAddress} should not be null or empty";
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

                var result = await _nylasService.DefaultsNylasAccountAsync(AccountId, emailAddress);

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

        [Authorize(Roles = "Cohealer")]
        [HttpPut("EnableConflictsCheckForExternalCalendars")]
        public async Task<IActionResult> EnableCheckCalendarConflictsAsync(List<string> emailAddresses)
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.EnableCheckCalendarConflictsForNylasAccountsAsync(AccountId, emailAddresses);

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

        [Authorize(Roles = "Cohealer")]
        [HttpPut("DisableConflictsCheckForExternalCalendar")]
        public async Task<IActionResult> DisableCheckCalendarConflictsAsync(string emailAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.DisableCheckCalendarConflictsForNylasAccountsAsync(AccountId, emailAddress);

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

        [Authorize(Roles = "Cohealer")]
        [HttpPut("EnableEventRemindersForExternalCalendar")]
        public async Task<IActionResult> EnableCalendarEventRemindersAsync(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                var errorMessage = $"{emailAddress} should not be null or empty";
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

                var result = await _nylasService.EnableEventRemindersForNylasAccountAsync(AccountId, emailAddress);

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

        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetExternalCalendarAccountsToCheckConflicts")]
        [ProducesResponseType(200, Type = typeof(List<ExternalCalendarAccountViewModel>))]
        public async Task<IActionResult> GetNylasAccountsWithCheckConflictsEnabledForCohereAccountAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.GetNylasAccountsWithCheckConflictsEnabledForCohereAccountAsync(AccountId);

                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetExternalCalendarAccountToSendReminders")]
        [ProducesResponseType(200, Type = typeof(ExternalCalendarAccountViewModel))]
        public async Task<IActionResult> GetNylasAccountWithEventRemindersEnabledForCohereAccountAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.GetNylasAccountWithEventRemindersEnabledForCohereAccountAsync(AccountId);

                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetAllExternalCalendarAccounts")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ExternalCalendarAccountViewModel>))]
        public async Task<IActionResult> GetAllNylasAccountsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.GetNylasAccountsForCohereAccountAsync(AccountId);

                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        /// <summary>
        /// Get Busy Time Ranges
        /// </summary>
        /// <param name="startTime">From Time in cohealer timezone</param>
        /// <param name="endTime">End Time in cohealer timezone</param>
        /// <returns>Array of busy time time ranges</returns>
        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetBusyTime")]
        public async Task<IActionResult> GetBusyTime(DateTimeOffset? startTime, DateTimeOffset? endTime)
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = "accountId should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _nylasService.GetBusyTimes(AccountId, startTime, endTime);

                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode((int)HttpStatusCode.InternalServerError);
            }
        }

        [HttpGet("webhook")]
        public IActionResult CalendarWebhookGet([FromQuery] string challenge)
        {
            return Ok(challenge);
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> CalendarWebhookPost([FromBody]JsonElement json)
        {
            var requestBody = json.ToString();

            if (Request.Headers.TryGetValue("X-Nylas-Signature", out var signature) && !IsValidRequest(signature, requestBody))
            {
                return Forbid();
            }

            var model = JsonSerializer.Deserialize<NylasWebhook>(requestBody);

            await _nylasService.InvalidateCache(model.Deltas);
            return Ok();
        }

        private static byte[] HashHMAC(byte[] key, byte[] message)
        {
            var hash = new HMACSHA256(key);
            return hash.ComputeHash(message);
        }

        private static byte[] StringEncode(string text)
        {
            var encoding = new UTF8Encoding();
            return encoding.GetBytes(text);
        }

        private static string HashHMACHex(string key, string message)
        {
            byte[] hash = HashHMAC(StringEncode(key), StringEncode(message));
            return HashEncode(hash);
        }

        private static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }

        private bool IsValidRequest(string signature, string rawBody)
        {
            var hash = HashHMACHex(_nylasClientSecret, rawBody);

            var isValid = hash == signature;

            _logger.Log(LogLevel.Information, $"NylasValidator request valid: {isValid}");
            return isValid;
        }
    }
}
