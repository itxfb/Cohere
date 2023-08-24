using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Abstractions;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("[controller]")]

    //[Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : CohereController
    {
        private readonly IAuthService _authService;
        private readonly ITokenGenerator _tokenGenerator;
        private readonly IValidator<LoginViewModel> _loginValidator;
        private readonly IMapper _mapper;
        private readonly IProfilePageService _profilePageService;
        private readonly IUserService<UserViewModel, User> _userService;

        public AuthController(
            IAuthService authService,
            ITokenGenerator tokenGenerator,
            IValidator<LoginViewModel> loginValidator,
            IMapper mapper, 
            IProfilePageService profilePageService, 
            IUserService<UserViewModel, User> userService)
            :base(tokenGenerator)
        {
            _authService = authService;
            _tokenGenerator = tokenGenerator;
            _loginValidator = loginValidator;
            _mapper = mapper; 
            _profilePageService = profilePageService;
            _userService = userService;
        }

        // POST: /Auth/SignIn
        [AllowAnonymous]
        [HttpPost("SignIn")]
        public async Task<IActionResult> SignIn([FromBody] LoginViewModel login)
        {
            var validationResult = await _loginValidator.ValidateAsync(login);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            IActionResult response;
            var result = await _authService.SignInAsync(login, false);

            if (result.Succeeded)
            {
                var accountAndUser = (AccountAndUserAggregatedViewModel)result.Payload;
                if (!accountAndUser.User.IsPermissionsUpdated)
                {

                }
                AddOAuthTokenToResponseHeader(accountAndUser.Account);
                var profileResult = await _profilePageService.GetProfilePage(accountAndUser.User.AccountId);
                if (profileResult != null)
                {
                    var profileViewModel = _mapper.Map<ProfilePageViewModel>(profileResult);
                    accountAndUser.User.ProfilePageViewModel = profileViewModel;
                }
                var progressBarData = await _userService.GetAndSaveUserProgressbarData(accountAndUser.User);
                if (progressBarData.Succeeded)
                {
                    accountAndUser.User.UserProgressbarData = (Dictionary<string, bool>)progressBarData.Payload;
                    accountAndUser.User.ProgressBarPercentage = _userService.GetProgressbarPercentage(accountAndUser?.User?.UserProgressbarData);
                }
                response = Ok(accountAndUser);
            }
            else
            {
                response = BadRequest(new ErrorInfo { Message = result.Message });
            }

            return response;
        }

        [Authorize]
        [HttpGet("GetAccountInfo")]
        public async Task<IActionResult> GetAccountInfo()
        {
            var accountData = await _authService.GetUserData(AccountId);

            return accountData.ToActionResult();
        }
    }
}