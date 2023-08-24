using System;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Abstractions;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Utils.Validators;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Enums;
using Cohere.Entity.Infrastructure.Options;
using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountController : CohereController
    {
        private readonly IServiceAsync<AccountViewModel, Account> _accountService;
        private readonly IAccountManager _accountManager;
        private readonly IValidator<AccountViewModel> _accountValidator;
        private readonly IValidator<ChangePasswordViewModel> _changePasswordValidator;
        private readonly IValidator<TokenVerificationViewModel> _tokenVerificationValidator;
        private readonly IValidator<RestorePasswordViewModel> _restorePasswordValidator;
        private readonly IValidator<RestoreBySecurityAnswersViewModel> _restorePasswordBySecurityAnswersValidator;
        private readonly ITokenGenerator _tokenGenerator;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IServiceAsync<AccountViewModel, Account> accountService,
            IAccountManager accountManager,
            IValidator<AccountViewModel> accountValidator,
            IValidator<ChangePasswordViewModel> changePasswordValidator,
            IValidator<RestorePasswordViewModel> restorePasswordValidator,
            IValidator<TokenVerificationViewModel> tokenVerificationValidator,
            IValidator<RestoreBySecurityAnswersViewModel> restorePasswordBySecurityAnswersValidator,
            ITokenGenerator tokenGenerator,
            ILogger<AccountController> logger):base(tokenGenerator)
        {
            _accountService = accountService;
            _accountManager = accountManager;
            _accountValidator = accountValidator;
            _changePasswordValidator = changePasswordValidator;
            _restorePasswordValidator = restorePasswordValidator;
            _tokenVerificationValidator = tokenVerificationValidator;
            _restorePasswordBySecurityAnswersValidator = restorePasswordBySecurityAnswersValidator;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
        }

        // GET: /Account
        [Authorize(Roles = "Admin, SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _accountService.GetAll();
            return Ok(items);
        }

        // GET: /Account/accountId
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo("The Id is empty"));
            }

            var isOwnAccountOrAdmin = id == AccountId || User.IsInRole(Roles.Admin.ToString()) || User.IsInRole(Roles.SuperAdmin.ToString());
            if (!isOwnAccountOrAdmin)
            {
                return Forbid();
            }

            var accountVm = await _accountService.GetOne(id);
            if (accountVm == null)
            {
                _logger.LogError($"GetById({id}) NOT FOUND");
                return NotFound();
            }
            AddOAuthTokenToResponseHeader(accountVm);
            return Ok(accountVm);
        }

        // POST: /Account
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] AccountViewModel account)
        {
            if (account == null)
            {
                return BadRequest();
            }

            var validationResult = await _accountValidator.ValidateAsync(account);

            if (validationResult.IsValid && Email.IsValid(account.Email))
            {
                try
                {
                    var result = await _accountService.Insert(account);
                    if (result.Succeeded)
                    {
                        var accountCreated = (AccountViewModel)result.Payload;
                        AddOAuthTokenToResponseHeader(accountCreated);
                        return Created($"Account/{accountCreated.Id}", accountCreated); //HTTP201 Resource created
                    }

                    _logger.LogError(
                        $"Unable to register account: {result.Message}",
                        account.Email,
                        DateTime.Now.ToString("F"));
                    return BadRequest(new ErrorInfo { Message = result.Message });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception occured during registration account with email {account.Email}: {ex.Message}");
                    throw;
                }
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // GET: /Account/CheckEmail/email
        [HttpGet("CheckEmail/{email}")]
        public async Task<IActionResult> CheckEmailAvailability(string email)
        {
            if (email == null)
            {
                return BadRequest(new ErrorInfo { Message = "Email cannot be null" });
            }

            var isEmailAvailable = await _accountManager.IsEmailAvailableForRegistration(email);
            if (isEmailAvailable)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo { Message = "Current Email is registered! Try another one!" });
        }

        [Authorize]
        [HttpPatch("CoachLoginInfo")]
        public async Task<IActionResult> Update(string id, [FromBody] CoachLoginInfo coachLoginInfo)
        {
            var result = await _accountManager.UpdateCoachLoginInfo(id, coachLoginInfo);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        // PUT: /Account/accountId
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] AccountViewModel account)
        {
            if (account == null || account.Id != id)
            {
                return BadRequest(new ErrorInfo { Message = "Account is null or Id in body doesn't match Id in route parameter" });
            }

            if (AccountId != id)
            {
                Forbid();
            }

            account.Password = "SamplePasswordToStepOverValidation!1";
            var validationResult = await _accountValidator.ValidateAsync(account);

            if (validationResult.IsValid)
            {
                var result = await _accountService.Update(account);
                if (result.Succeeded)
                {
                    var accountUpdated = (AccountViewModel)result.Payload;
                    AddOAuthTokenToResponseHeader(accountUpdated);
                    return Accepted(accountUpdated);
                }

                _logger.LogError($"Unable to update account: {result.Message}", account.Id, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message }); // Not Modified
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        //POST /Account/ConfirmEmail
        [HttpPost("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail([FromBody] TokenVerificationViewModel verificationModel)
        {
            if (verificationModel == null)
            {
                return BadRequest();
            }

            var validationResult = await _tokenVerificationValidator.ValidateAsync(verificationModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _accountManager.ConfirmAccountEmailAsync(verificationModel);
            if (result.Succeeded)
            {
                return Ok(result.Message);
            }

            _logger.LogError($"Unable to confirm email {result.Message} for email", verificationModel.Email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        // POST: /Account/ChangePassword
        [Authorize]
        [HttpPost("ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel changePasswordViewModel)
        {
            if (changePasswordViewModel == null)
            {
                return BadRequest(new ErrorInfo("Change password model must not be null"));
            }

            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find NameIdentifier in Jwt token"));
            }

            var validationResult = await _changePasswordValidator.ValidateAsync(changePasswordViewModel);

            if (validationResult.IsValid)
            {
                var result = await _accountManager.ChangePassword(changePasswordViewModel, AccountId);
                if (result.Succeeded)
                {
                    return Ok();
                }

                _logger.LogError($"Unable to change password {result.Message} for email", changePasswordViewModel.Email, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        [HttpGet("RestorePassword/{email}/RequestLink")]
        public async Task<IActionResult> RestorePasswordByLinkRequest([FromRoute] string email)
        {
            if (email == null)
            {
                return BadRequest(new ErrorInfo { Message = "Email cannot be null" });
            }

            var result = await _accountManager.RestorePasswordByLinkRequestAsync(email);
            if (result.Succeeded)
            {
                return Ok(result.Message);
            }

            _logger.LogError($"Unable to request password restoration via link {result.Message} for email", email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpGet("RestorePassword/{email}/RequestAnswers")]
        public async Task<IActionResult> RestorePasswordByAnswersRequest([FromRoute] string email)
        {
            if (email == null)
            {
                return BadRequest(new ErrorInfo { Message = "Email cannot be null" });
            }

            var result = await _accountManager.RestorePasswordByAnswersRequestAsync(email);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            _logger.LogError($"Unable to request password restoration via security answers {result.Message} for email", email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPost("RestorePasswordLinkVerification")]
        public async Task<IActionResult> RestorePasswordByLinkVerification([FromBody] TokenVerificationViewModel verificationModel)
        {
            if (verificationModel == null)
            {
                return BadRequest();
            }

            var validationResult = await _tokenVerificationValidator.ValidateAsync(verificationModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _accountManager.VerifyPasswordRestorationLinkAsync(verificationModel);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            _logger.LogError($"Unable to verify password restoration via link {result.Message} for email", verificationModel.Email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPost("RestorePasswordAnswersVerification")]
        public async Task<IActionResult> RestorePasswordByAnswersVerification([FromBody] RestoreBySecurityAnswersViewModel securityAnswersModel)
        {
            if (securityAnswersModel == null)
            {
                return BadRequest();
            }

            var validationResult = await _restorePasswordBySecurityAnswersValidator.ValidateAsync(securityAnswersModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _accountManager.VerifySecurityAnswersAsync(securityAnswersModel);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            _logger.LogError($"Unable to verify password restoration via security answers {result.Message} for email", securityAnswersModel.Email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPost("RestorePassword")]
        public async Task<IActionResult> RestorePassword([FromBody] RestorePasswordViewModel restoreModel)
        {
            if (restoreModel == null)
            {
                return BadRequest();
            }

            var validationResult = await _restorePasswordValidator.ValidateAsync(restoreModel);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _accountManager.RestorePasswordAsync(restoreModel);
            if (result.Succeeded)
            {
                return Ok();
            }

            _logger.LogError($"Unable to restore password {result.Message} for email", restoreModel.Email, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [Authorize]
        [HttpPost("RequestEmailConfirmation")]
        public async Task<IActionResult> RequestEmailConfirmation()
        {
            var requestResult = await _accountManager.RequestEmailConfirmationAsync(AccountId);

            if (!requestResult.Succeeded)
            {
                return BadRequest(new ErrorInfo(requestResult.Message));
            }

            return Ok();
        }

        [Authorize]
        [HttpGet("CheckVideoTest")]
        public async Task<IActionResult> CheckVideoTest()
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var isItFirstTime = await _accountManager.IsVideoTestFirstTime(AccountId);

            return Ok(isItFirstTime);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("HidePaidTierOptionBanner")]
        public async Task<IActionResult> HidePaidTierOptionBanner()
        {
            await _accountManager.HidePaidTierOptionBanner(AccountId);
            return Ok();
        }
    }
}
