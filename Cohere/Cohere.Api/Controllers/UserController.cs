using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using AutoMapper;
using Cohere.Api.Controllers.Models;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Abstractions;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cohere.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("[controller]")]
    [ApiController]
    public class UserController : CohereController
    {
        private readonly IUserService<UserViewModel, User> _userService;
        private readonly IAccountService<AccountViewModel, Account> _accountService;
        private readonly IValidator<UserViewModel> _userValidator;
        private readonly ITokenGenerator _tokenGenerator;
        private readonly ILogger<UserController> _logger;
        private readonly IRoleSwitchingService _roleSwitchingService;
        private readonly IContributionAccessService _contributionAccessService;
        private readonly INotificationService _notificationService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly StripeAccountService _stripeAccountService;
        private readonly IContentService _contentService;
        private readonly IMapper _mapper;
        private readonly IProfilePageService _profilePageService;
        private readonly IValidator<ProfilePageViewModel> _profilePageValidator;

        public UserController(
            IUserService<UserViewModel, User> userService,
            IAccountService<AccountViewModel, Account> accountService,
            IValidator<UserViewModel> userValidator,
            ITokenGenerator tokenGenerator,
            ILogger<UserController> logger,
            IRoleSwitchingService roleSwitchingService,
            IContributionAccessService contributionAccessService,
            INotificationService notificationService,
            IActiveCampaignService activeCampaignService,
            IUnitOfWork unitOfWork,
            StripeAccountService stripeAccountService,
            IContentService contentService,
            IMapper mapper,
            IProfilePageService profilePageService,
            IValidator<ProfilePageViewModel> profilePageValidator) : base(tokenGenerator)
        {
            _notificationService = notificationService;
            _activeCampaignService = activeCampaignService;
            _userService = userService;
            _accountService = accountService;
            _userValidator = userValidator;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
            _roleSwitchingService = roleSwitchingService;
            _contributionAccessService = contributionAccessService;
            _unitOfWork = unitOfWork;
            _stripeAccountService = stripeAccountService;
            _mapper = mapper;
            _contentService = contentService;
            _profilePageService = profilePageService;
            _profilePageValidator = profilePageValidator;
        }

        //GET: /User
        [Authorize(Roles = "Admin, SuperAdmin")]
        [HttpGet]
        public async Task<IEnumerable<UserViewModel>> GetAll()
        {
            var items = await _userService.GetAll();
            return items;
        }

        //GET: /User/userId
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetOne(id);
            if (user == null)
            {
                _logger.LogError($"User GetById {id}) NOT FOUND", DateTime.Now.ToString("F"));
                return NotFound();
            }
            var profileResult = await _profilePageService.GetProfilePage(user.AccountId);
            if (profileResult != null)
            {
                var profileViewModel = _mapper.Map<ProfilePageViewModel>(profileResult);
                user.ProfilePageViewModel = profileViewModel;
            }
            var result = await _userService.GetAndSaveUserProgressbarData(user);
            if (result.Succeeded)
            {
                user.UserProgressbarData = (Dictionary<string, bool>)result.Payload;
                user.ProgressBarPercentage = _userService.GetProgressbarPercentage(user?.UserProgressbarData);
            }
            return Ok(user);
        }

        // POST /User
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpPost]
        public async Task<IActionResult> AddUserInfo([FromBody] UserViewModel user)
        {
            if (user == null)
            {
                return BadRequest();
            }

            var validationResult = await _userValidator.ValidateAsync(user);

            if (validationResult.IsValid)
            {
                user.BirthDate = (DateTime.Today).AddYears(-20);
                var result = await _userService.Insert(user);
                if (result.Succeeded)
                {
                    var accountAndUser = (AccountAndUserAggregatedViewModel)result.Payload;
                    AddOAuthTokenToResponseHeader(accountAndUser.Account);

                    // Account (coach or/and client) is activated
                    //var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("US Mountain Standard Time");
                    //DateTime nowTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, timeZoneInfo);
                    DateTime nowTime = DateTime.Now;

                    var acContact = new ActiveCampaignContact()
                    {
                        Email = accountAndUser.Account.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                    };

                    string cohereAccountType = user.IsCohealer ?
                            EnumHelper<CohereAccountType>.GetDisplayValue(CohereAccountType.CoachAccountActivated) :
                            EnumHelper<CohereAccountType>.GetDisplayValue(CohereAccountType.ClientAccountActivated);

                    if (user.IsCohealer)
                    {
                        _activeCampaignService.SendActiveCampaignEvents(acContact, cohereAccountType, nowTime.ToString("MM/dd/yyyy"), user.FirstName + " " + user.LastName);

                        if (user.IsCohealer)
                        {
                            // handled in Active Campaign
                            //await _notificationService.SendInstructionsToNewCohealerAsync(user.AccountId);
                        }

                        // update active campaign source of referral
                        var account = await _accountService.GetOne(user?.AccountId);
                        if (account != null)
                        {
                            if (account?.InvitedBy != null)
                            {
                                var referedByAccount = await _accountService.GetOne(account.InvitedBy);
                                if (!string.IsNullOrWhiteSpace(referedByAccount?.Email))
                                {
                                    ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
                                    ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                                    {
                                        CohereAccountId = account?.Id,
                                        LastCohereActivity = DateTime.UtcNow.ToString("MM/dd/yyyy"),
                                        InvitedBy = referedByAccount.Email
                                    };
                                    _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
                                }
                            }
                        }
                    }
                    var progressBarData = await _userService.GetAndSaveUserProgressbarData(user);
                    if (progressBarData.Succeeded)
                    {
                        accountAndUser.User.UserProgressbarData = (Dictionary<string, bool>)progressBarData.Payload;
                        accountAndUser.User.ProgressBarPercentage = _userService.GetProgressbarPercentage(accountAndUser?.User?.UserProgressbarData);
                    }

                    return Created($"User/{accountAndUser.User.Id}", accountAndUser); //HTTP201 Resource created
                }

                _logger.LogError($"Unable to add user: {result.Message} for user with AccountId", user.AccountId, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // PUT: /User/userId
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUserInfo(string id, [FromBody] UserViewModel user)
        {
            if (user == null || user.Id != id)
            {
                return BadRequest(new ErrorInfo { Message = "User is null or Id in body doesn't match Id in route parameter" });
            }
            var Activeuser = await _userService.GetOne(id);
            if (Activeuser == null)
            {
                return BadRequest(new ErrorInfo { Message = "User is null or Id in body doesn't match Id in route parameter" });
            }
            if (!string.IsNullOrEmpty(Activeuser.ProfileLinkName) && string.IsNullOrEmpty(user.ProfileLinkName))
            {
                user.ProfileLinkName = Activeuser.ProfileLinkName;
            }
            bool isprofile = true;
            ProfilePageViewModel profilePageModel = user.ProfilePageViewModel;
            if (profilePageModel != null)
            {
                var validationProfileResult = await _profilePageValidator.ValidateAsync(profilePageModel);
                if (!validationProfileResult.IsValid)
                {
                    isprofile = false;
                    return BadRequest(new ErrorInfo { Message = validationProfileResult.ToString() });
                }
            }
            var validationResult = await _userValidator.ValidateAsync(user);
            if (validationResult.IsValid && isprofile)
            {
                var _userObject = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == id);
                var requestedAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == _userObject.AccountId);

                var isDefaultStripeAccountCreated = (Constants.DefaultStripeAccount == Constants.Stripe.AccountType.Standard && !string.IsNullOrEmpty(_userObject.StripeStandardAccountId))
                                                   || (Constants.DefaultStripeAccount == Constants.Stripe.AccountType.Custom && !string.IsNullOrEmpty(_userObject.ConnectedStripeAccountId));

                if (!isDefaultStripeAccountCreated && !string.IsNullOrEmpty(user.CountryId))
                {
                    var country = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(u => u.Id == user.CountryId);

                    var filestream = await _contentService.GetFileFromS3Async(_userObject.CustomLogo);
                    var stripeAcccountResult = await _stripeAccountService.CreateDefaultSripeAccountforUser(
                    requestedAccount.Email,
                    country.Alpha2Code, _mapper.Map<User>(user), filestream);

                    if (stripeAcccountResult.Failed)
                    {
                        // return createConnectedAccountResult;
                    }
                }

                var result = await _userService.Update(user);

                if (_userService.IsBrandingColorChnaged(_userObject, user))
                {
                    _stripeAccountService.SetCustomColorForCheckout(_userObject.ConnectedStripeAccountId, user.BrandingColors["PrimaryColorCode"], user.BrandingColors["AccentColorCode"], _userObject.StripeStandardAccountId);
                }


                if (_userService.IsBrandingLogoChanged(_userObject, user))
                {
                    var filestream = await _contentService.GetFileFromS3Async(user.CustomLogo);
                    if (filestream != null)
                        _stripeAccountService.SetCustomLogoForCheckout(_userObject.ConnectedStripeAccountId, filestream, _userObject.StripeStandardAccountId);
                }

                if (result.Succeeded)
                {
                    var accountAndUser = (AccountAndUserAggregatedViewModel)result.Payload;
                    AddOAuthTokenToResponseHeader(accountAndUser.Account);

                    if (profilePageModel != null && isprofile)
                    {
                        if (profilePageModel.UpdationAllowed)
                        {
                            var profileResult = await _profilePageService.InsertOrUpdateProfilePage(profilePageModel, AccountId);
                            if (profileResult.Succeeded)
                            {
                                accountAndUser.User.ProfilePageViewModel = (ProfilePageViewModel)profileResult.Payload;
                            }
                        }
                        else
                        {
                            var existingProfilePage = await _profilePageService.GetProfilePage(Activeuser.AccountId);
                            if (existingProfilePage != null)
                            {
                                var profileViewModel = _mapper.Map<ProfilePageViewModel>(existingProfilePage);
                                accountAndUser.User.ProfilePageViewModel = profileViewModel;
                            }
                        }

                    }
                    return Accepted(accountAndUser);
                }

                _logger.LogError($"Unable to update user: {result.Message} with Id: ", user.Id, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message }); //Not Modified
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        [Authorize]
        [HttpPut("UpdateUserProfileColors")]
        public async Task<IActionResult> UpdateUserProfileColors([FromBody] BrandingColorsDTO obj)
        {
            var _userObject = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == obj.Id);

            if (_userObject == null || _userObject.Id != obj.Id)
            {
                return BadRequest(new ErrorInfo { Message = "User is null or Id in body doesn't match Id in route parameter" });
            }

            var result = await _userService.UpdateUserProfileColors(obj.Id, obj.BrandingColors, obj.CustomLogo);

            if (result.Succeeded)
            {

                return Ok(result.Payload);

            }

            _logger.LogError($"Unable to update user: {result.Message} with Id: ", _userObject.Id, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message }); //Not Modified

        }

        [Authorize]
        [HttpPut("UpdateUserAttributes")]
        public async Task<IActionResult> UpdateUserAttributes([FromBody] UserDTO userObject)
        {
            var _userObject = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userObject.Id);

            if (_userObject == null || _userObject.Id != userObject.Id)
            {
                return BadRequest(new ErrorInfo { Message = "User is null or Id in body doesn't match Id in route parameter" });
            }

            var result = await _userService.UpdateUserFromDynamicObjectAsync(userObject);

            if (result.Succeeded)
            {

                return Ok(result.Payload);

            }

            _logger.LogError($"Unable to update user: {result.Message} with Id: ", _userObject.Id, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message }); //Not Modified

        }
        
        // DELETE: /User/userId
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserInfo(string id)
        {
            var result = await _userService.Delete(id);

            if (result.Succeeded)
            {
                return NoContent(); //No Content 204
            }

            _logger.LogError($"Unable to delete user: {result.Message} with userId: ", id, DateTime.Now.ToString("F"));
            return NotFound(new ErrorInfo { Message = result.Message }); // Not Found 404
        }

        [HttpGet("GetCohealerIcon/{id}")]
        public async Task<IActionResult> GetCohealerIcon(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var cohealer = await _userService.GetCohealerIconByContributionId(id);
                if (cohealer == null)
                {
                    _logger.LogError($"GetCohealer {id} NOT FOUND", DateTime.Now.ToString("F"));
                    return NotFound();
                }

                if (cohealer.AvatarUrl != null)
                {
                    return Ok(cohealer.AvatarUrl);
                }
                else
                {
                    return Ok(new ErrorInfo("Unable to find profile pictur please upload one."));
                }
            }

            return BadRequest(new ErrorInfo("Cohealer Id is null"));
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("SwitchFromCoachToClient")]
        public async Task<IActionResult> SwitchFromCoachToClient()
        {
            var result = await _roleSwitchingService.SwitchFromCoachToClient(AccountId);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("SwitchFromClientToCoach")]
        public async Task<IActionResult> SwitchFromClientToCoach([FromBody] SwitchFromClientToCoachViewModel model)
        {
            var result = await _roleSwitchingService.SwitchFromClientToCoach(AccountId, model);

            return result.ToActionResult();
        }

        [Authorize]
        [HttpPost("SaveUserSocialLastReadTime")]
        public async Task<IActionResult> SaveUserSocialLastReadTime([FromBody] UserSocialLastReadModel model)
        {
            var result = await _userService.SaveUserSocialLastReadTime(model.UserId, model.ContributionId);

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [Authorize]
        [HttpGet("GetNonReadedPostsCount")]
        public async Task<IActionResult> GetCountsOfNonReadedSocialPostsForContributions([FromQuery] string userId, [FromQuery] IEnumerable<string> contributionIds)
        {
            var result = await _userService.GetCountsOfNonReadedSocialPostsForContributions(userId, contributionIds);

            return result.ToActionResult();
        }
        [Authorize]
        [HttpPost("GetNonReadedPostsTotalCount")]
        public async Task<IActionResult> GetTotalCountsOfNonReadedSocialPostsForContributions(UnReadPostCountModel model)
        {
            var result = await _userService.GetTotalCountsOfNonReadedSocialPostsForContributions(model.UserId, model.ContributionIds);
            return result.ToActionResult();
        }
        [Authorize(Roles = "Cohealer")]
        [HttpGet("getmessage")]
        public async Task<IActionResult> GetPopupMessage()
        {
            var message = await _userService.GetPopupMessage();
            return message.ToActionResult();
        }
        [Authorize]
        [HttpGet("GetUserDetails")]
        public async Task<IActionResult> GetUserDetail(string userId)
        {

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("UserId is null or empty");
            }
            var model = await _userService.GetUserDetails(userId);
            return model.ToActionResult();
        }

        [Authorize]
        [HttpPost("AddProfileLinkName")]
        public async Task<IActionResult> AddProfileLinkName(string profileName)
        {
            try
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    var errorMessage = $"{profileName} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest(errorMessage);
                }
                var result = await _userService.AddProfileLinkName(AccountId, profileName);
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
        [HttpGet("GetClientAndContributionDetailForZapier")]
        public async Task<IActionResult> GetClientAndContributionDetailForZapier()
        {
            var model = await _userService.GetClientAndContributionDetailForZapier(AccountId);
            if (model == null)
            {
                return BadRequest();
            }
            return Ok(model.Payload);
        }


        [Authorize(Roles = "Cohealer")]
        [HttpPost("CreateStripeStandardAccount")]
        public async Task<IActionResult> CreateStripeStandardAccount()
        {
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == AccountId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == AccountId);
            if (coachUser == null || coachAccount == null || string.IsNullOrEmpty(coachUser.CountryId))
            {
                return BadRequest("No account exist or user must select the country for standard account");
            }
            var country = await _unitOfWork.GetGenericRepositoryAsync<Country>().GetOne(c => c.Id == coachUser.CountryId);
            var filestream = await _contentService.GetFileFromS3Async(coachUser.CustomLogo);
            var result = await _stripeAccountService.CreateStandardConnectAccountAsync(coachAccount.Email, country.Alpha2Code, coachUser,filestream, createFromDashboard: true);
            if (result.Succeeded)
            {
                return result.ToActionResult();
            }
            return BadRequest(result.Message);
        }

        [HttpPost("CheckProfileLinkName")]
        public async Task<IActionResult> CheckProfileLinkName(string profileName)
        {
             try
                {
                    if (string.IsNullOrEmpty(profileName))
                    {
                        var errorMessage = $"{profileName} should not be null or empty";
                        _logger.LogError(errorMessage);
                        return BadRequest(errorMessage);
                    }
                    var result = await _userService.CheckProfileLinkName(profileName);
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
        [HttpPost("CreateStripeCustomAccount")]
        public async Task<IActionResult> CreateStripeCustomAccount()
        {
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == AccountId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == AccountId);
            if (coachUser == null || coachAccount == null || string.IsNullOrEmpty(coachUser.CountryId))
            {
                return BadRequest("No account exist or user must select the country for standard account");
            }
            var country = await _unitOfWork.GetGenericRepositoryAsync<Country>().GetOne(c => c.Id == coachUser.CountryId);
            var filestream = await _contentService.GetFileFromS3Async(coachUser.CustomLogo);
            var result = await _stripeAccountService.CreateCustomConnectAccountAsync(coachAccount.Email, country.Alpha2Code, coachUser.IsBetaUser, coachUser, filestream, createFromDashboard: true);
            if (result.Succeeded)
            {
                return result.ToActionResult();
            }
            return BadRequest(result.Message);
        }
    }
}
