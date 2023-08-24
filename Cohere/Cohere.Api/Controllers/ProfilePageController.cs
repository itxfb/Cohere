using Cohere.Api.Utils;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using CsvHelper;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ProfilePageController : CohereController
    {

        private readonly IProfilePageService _profilePageService;
        private readonly IValidator<ProfilePageViewModel> _profilePageValidator;
        private readonly ILogger<ProfilePageController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public ProfilePageController(IProfilePageService profilePageService, IValidator<ProfilePageViewModel> profilePageValidator, ILogger<ProfilePageController> logger,
            IUnitOfWork unitOfWork)
        {
            _profilePageService = profilePageService;
            _profilePageValidator = profilePageValidator;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        [Authorize]
        [HttpPost("AddOrUpdateProfilePage")]
        public async Task<IActionResult> AddOrUpdateProfilePage([FromBody] ProfilePageViewModel model)
        {

            try
            {

                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var validationResult = await _profilePageValidator.ValidateAsync(model);

                if (validationResult.IsValid)
                {
                    var result = await _profilePageService.InsertOrUpdateProfilePage(model, AccountId);
                    if (result.Succeeded)
                    {
                        return Ok();
                    }
                    return BadRequest(result.Message);
                }
                else
                {
                    return BadRequest(validationResult.Errors);
                }


            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize]
        [HttpGet("GetProfilePageInfo")]
        public async Task<IActionResult> GetProfilePageInfo()
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var profilePage = await _profilePageService.GetProfilePage(AccountId);
                if (profilePage != null)
                {
                    return Ok(profilePage);
                }
                return NotFound();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [HttpGet("GetProfileLinkNameByContributionId")]
        public async Task<IActionResult> GetProfileLinkNameByContributionId(string ContributionId)
        {
            try
            {
                if (String.IsNullOrEmpty(ContributionId))
                {
                    var errorMessage = $"{ContributionId} ContributionId should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _profilePageService.GetProfileLinkNameByContributionId(ContributionId);
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

        [HttpGet("GetProfilePageInfoByName")]
        public async Task<IActionResult> GetProfilePageInfoByName(string profileName)
        {
            try
            {
                if (String.IsNullOrEmpty(profileName))
                {
                    var errorMessage = $"{profileName} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _profilePageService.GetProfilePageByName(profileName);
                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }
                return NotFound();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize]
        [HttpPost("GetAllFollowers")]

        public async Task<IActionResult> GetAllFollowers()
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var followers = await _profilePageService.GetAllFollowers(AccountId);
                if (followers != null)
                {
                    return Ok(followers);
                }
                return NotFound();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }

        }



        [Authorize]
        [HttpPost("FollowProfile")]

        public async Task<IActionResult> FollowProfile(string profileAccountId)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                if (String.IsNullOrEmpty(profileAccountId))
                {
                    var errorMessage = $"{profileAccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest(errorMessage);
                }

                var result = await _profilePageService.AddFollowerToProfile(AccountId, profileAccountId);

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


        [Authorize]
        [HttpPost("UnFollowProfile")]

        public async Task<IActionResult> UnFollowProfile(string profileAccountId)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                if (String.IsNullOrEmpty(profileAccountId))
                {
                    var errorMessage = $"{profileAccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest(errorMessage);
                }

                var result = await _profilePageService.RemoveFollowerFromProfile(AccountId, profileAccountId);

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



        [Authorize]
        [HttpGet("GetAllCustomLinks")]
        public async Task<IActionResult> GetAllCustomLinks()
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _profilePageService.GetAllCustomLinks(AccountId);

                if (result != null)
                {
                    return Ok(result);
                }

                return NotFound();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }

        }


        [Authorize]
        [HttpPost("AddCustomLink")]
        public async Task<IActionResult> AddCustomLink(List<CustomLinksViewModel> viewModel)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }


                var result = await _profilePageService.AddCustomLink(viewModel, AccountId);

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


        [Authorize]
        [HttpPut("UpdateCustomLink")]
        public async Task<IActionResult> UpdateCustomLink(CustomLinksViewModel viewModel, string uniqueName)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }


                var result = await _profilePageService.UpdateCustomLinkByUniqueName(viewModel, AccountId, uniqueName);

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


        [Authorize]
        [HttpPost("SwitchCustomLinkVisibility")]
        public async Task<IActionResult> SwitchCustomLinkVisibility(bool isVisible, string uniqueName)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }


                var result = await _profilePageService.SwitchCustomLinkVisibilty(isVisible, AccountId, uniqueName);

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

        [Authorize]
        [HttpPost("ExportProfileFollowersDetailsAsync")]
        public async Task<IActionResult> ExportProfileFollowersDetailsAsync(string accountId)
        {
            var getFollowersDetails = await _profilePageService.GetProfileFollowersDetailsAsync(accountId);

            await using var memoryStream = new MemoryStream();
            await using (var writer = new StreamWriter(memoryStream))
            await using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csvWriter.WriteRecordsAsync(getFollowersDetails);
            }

            return File(memoryStream.ToArray(), "text/csv", "my client list.csv");

        }
    }
}
