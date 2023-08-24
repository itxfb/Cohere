using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Cohere.Api.Controllers.Models;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils.Validators.Contribution;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using CsvHelper;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ResourceLibrary;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ContributionController : CohereController
    {
        private readonly IContributionService _contributionService;
        private readonly IValidator<AdminReviewNoteViewModel> _reviewNoteValidator;
        private readonly IValidator<SetClassAsCompletedViewModel> _setClassAsCompletedValidator;
        private readonly IValidator<SetAsCompletedViewModel> _setAsCompletedValidator;
        private readonly IValidator<BookSessionTimeViewModel> _selectSessionTimeValidator;
        private readonly IValidator<ShareContributionEmailViewModel> _shareContributionModelValidator;
        private readonly IValidator<EmailTemplatesViewModel> _emailtemplateValidator;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<ContributionController> _logger;
        private readonly IContributionBookingService _contributionBookingService;
        private readonly IContributionRootService _contributionRootService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly IUserService<Cohere.Domain.Models.User.UserViewModel, User> _userService;
        private readonly IAccountService<Cohere.Domain.Models.Account.AccountViewModel, Account> _accountService;
        private readonly IUnfinishedContributionValidator _unfinishedContributionValidator;
        private readonly IChatManager _chatManager;
        private readonly IValidator<ProfilePageViewModel> _profilePageValidator;
        private readonly IProfilePageService _profilePageService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public ContributionController(
            IContributionService contributionService,
            IValidator<AdminReviewNoteViewModel> reviewNoteValidator,
            IValidator<SetClassAsCompletedViewModel> setClassAsCompletedValidator,
            IValidator<SetAsCompletedViewModel> setAsCompletedValidator,
            IValidator<BookSessionTimeViewModel> selectSessionTimeValidator,
            IValidator<ShareContributionEmailViewModel> shareContributionModelValidator,
            IValidator<EmailTemplatesViewModel> emailtemplateValidator,
            IStringLocalizer<SharedResource> localizer,
            ILogger<ContributionController> logger,
            IContributionBookingService contributionBookingService,
            IContributionRootService contributionRootService,
            IActiveCampaignService activeCampaignService,
            IUserService<Cohere.Domain.Models.User.UserViewModel, User> userService,
            IAccountService<Cohere.Domain.Models.Account.AccountViewModel, Account> accountService,
            IUnfinishedContributionValidator unfinishedContributionValidator,
            IChatManager chatManager,
            IValidator<ProfilePageViewModel> profilePageValidator,
            IProfilePageService profilePageService,
            IUnitOfWork unitOfWork,
            INotificationService notificationService)
        {
            _contributionService = contributionService;
            _reviewNoteValidator = reviewNoteValidator;
            _setClassAsCompletedValidator = setClassAsCompletedValidator;
            _setAsCompletedValidator = setAsCompletedValidator;
            _selectSessionTimeValidator = selectSessionTimeValidator;
            _shareContributionModelValidator = shareContributionModelValidator;
            _localizer = localizer;
            _logger = logger;
            _contributionBookingService = contributionBookingService;
            _contributionRootService = contributionRootService;
            _activeCampaignService = activeCampaignService;
            _userService = userService;
            _accountService = accountService;
            _unfinishedContributionValidator = unfinishedContributionValidator;
            _chatManager = chatManager;
            _emailtemplateValidator = emailtemplateValidator;
            _profilePageValidator = profilePageValidator;
            _profilePageService = profilePageService;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        // GET: /Contribution/GetCohealerContribById/{id}
        [Authorize(Roles = "Cohealer,Admin,SuperAdmin")]
        [HttpGet("GetCohealerContribById/{id}")]
        public async Task<IActionResult> GetCohealerContribById(string id)
        {

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution Id is null"]));
            }

            var result = await _contributionService.GetCohealerContributionByIdAsync(id, AccountId);
            if (result.Succeeded)
            {
                //Set Deafult Email Templates
                var emailtemplate = await _contributionService.GetCustomTemplateByContributionId(id);
                if (emailtemplate == null)
                {
                    await _contributionService.SetDefaultEmailTemplatesData(AccountId, id);
                }
                return Ok((ContributionBaseViewModel)result.Payload);
            }

            if (result.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(new ErrorInfo(result.Message));
        }
        // GET: /Contribution/GetCohealerContribById/{id}
        [Authorize(Roles = "Cohealer,Admin,SuperAdmin")]
        [HttpGet("UpdatePublicGroupChatsToPrivate")]
        public async Task<IActionResult> UpdatePublicGroupChatsToPrivate()
        {

            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution Id is null"]));
            }

            var result = await _chatManager.UpdatePublicGroupChatsToPrivate();
            if (result.Succeeded)
            {
                return Ok(result.Message);
            }

            if (result.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(new ErrorInfo(result.Message));
        }
        // GET: /Contribution/GetClientContribById/{id}
        [HttpGet("GetClientContribById/{id}")]
        public async Task<IActionResult> GetClientContribById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution Id is null"]));
            }
            var contribution = await _contributionService.GetClientContributionByIdAsync(id, AccountId);
            //Set Deafult Email Templates
            var emailtemplate = await _contributionService.GetCustomTemplateByContributionId(id);
            if (emailtemplate == null)
            {
                await _contributionService.SetDefaultEmailTemplatesData(AccountId, id);
            }
            if (contribution is null)
            {
                _logger.LogError($"GetClientContribById {id} NOT FOUND", DateTime.Now.ToString("F"));
            }

            return Ok(contribution);
        }

        // GET: /Contribution/GetAllBoughtByUserId/{id}
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpGet("GetAllBoughtByUserId/{id}")]
        public async Task<IActionResult> GetBoughtByUserId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }

            var contributions = await _contributionService.GetForClientJourneyAsync(id);

            return Ok(contributions);
        }
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpGet("GetAllBoughtByUserIdUpdated/{id}")]
        public async Task<IActionResult> GetBoughtByUserIdUpdated(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }
            var contributions = await _contributionService.GetBoughtByUserIdUpdated(id);
            return Ok(contributions);
        }

        [Authorize(Roles = "Client")]
        [HttpGet("GetBoughtByType/{type}")]
        public async Task<IActionResult> GetBoughtByType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return BadRequest("type is null or empty");
            }

            try
            {
                var result = await _contributionService.GetClientContributionByType(AccountId, type);

                if (type == "ContributionMembership" && result.Count() <= 0)
                {
                    _logger.LogError($"{type} - No Contribution result found against accountId: {AccountId}");
                }

                DefaultContractResolver contractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                };

                var json = JsonConvert.SerializeObject(result, new JsonSerializerSettings
                {
                    ContractResolver = contractResolver,
                    Formatting = Formatting.Indented
                });

                return Ok(json);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, $"Exception {type} - No Contribution found against accountId: {AccountId}");
            }
            return Ok("Error Occurred");
        }

        // GET: /Contribution/GetClientPurchases/{clientUserId}
        [HttpGet("GetClientPurchases/{clientUserId}")]
        public async Task<IActionResult> GetClientPurchases(string clientUserId)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(clientUserId))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }

            var getPurchasesResult = await _contributionService.GetForAllClientPurchasesAsync(AccountId, clientUserId);
            if (getPurchasesResult.Succeeded)
            {
                return Ok((List<JourneyPagePurchaseViewModel>)getPurchasesResult.Payload);
            }

            return BadRequest(new ErrorInfo(getPurchasesResult.Message));
        }

        // GET: /Contribution/GetUpcomingCreatedByUserId/{id}/{type}
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpGet("GetUpcomingCreatedByUserId/{id}/{type}")]
        public async Task<IActionResult> GetUpcomingCreatedByUserId(string id, string type)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }

            var getContributionResult = await _contributionService.GetUpcomingCreatedByCohealerAsync(id, AccountId, type);

            if (!getContributionResult.Succeeded)
            {
                _logger.LogError(
                    $"GetUpcomingCreatedByCohealer {id}/{type} failed: {getContributionResult.Message}",
                    DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo(getContributionResult.Message));
            }

            GroupedTableContributionViewModel partnerContributions = null;
            if (type == nameof(ContributionCourse) || type == nameof(ContributionMembership) || type == nameof(ContributionCommunity))
            {
                var partnerContributionsResult = await _contributionService.GetPartnerContributions(AccountId, type);
                if (partnerContributionsResult.Succeeded)
                {
                    partnerContributions = partnerContributionsResult.Payload;
                }
            }

            var tableResult = (GroupedTableContributionViewModel)getContributionResult.Payload;
            if (partnerContributions != null)
            {
                var userContributions = tableResult.Contributions.ToList();
                userContributions.AddRange(partnerContributions.Contributions);
                tableResult.Contributions = userContributions;

                var userUpcomingSessions = tableResult.UpcomingSessions.ToList();
                userUpcomingSessions.AddRange(partnerContributions.UpcomingSessions);
                tableResult.UpcomingSessions = userUpcomingSessions.OrderBy(x => x.StartTime);
            }

            if (!tableResult.Contributions.Any())
            {
                return NotFound();
            }
            return Ok(getContributionResult.Payload);

        }

        // GET: /Contribution/GetUpcomingCreatedByUserId/{id}
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpGet("GetUpcomingCreatedByUserId/{id}")]
        public async Task<IActionResult> GetUpcomingCreatedByUserId(string id, int? skip, int? take, string orderBy)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }

            orderBy = string.IsNullOrEmpty(orderBy) ? "Asc" : orderBy;
            if (!Enum.TryParse<OrderByEnum>(orderBy, out var orderByEnum))
            {
                return BadRequest();
            }

            var getContributionResult = await _contributionService.GetUpcomingCreatedByCohealerAsync(id, AccountId, skip, take, orderByEnum);

            if (!getContributionResult.Succeeded)
            {
                _logger.LogError(
                    $"GetUpcomingCreatedByCohealer {id} failed: {getContributionResult.Message}",
                    DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo(getContributionResult.Message));
            }

            var allContributionsModel = (AllUpcomingContributionsForCohealer)getContributionResult.Payload;

            var partnerContributionsResult = await _contributionService.GetPartnerContributions(AccountId, null, new List<ContributionStatuses> { ContributionStatuses.Approved, ContributionStatuses.Draft, ContributionStatuses.InReview, ContributionStatuses.Unfinished });

            if (partnerContributionsResult.Succeeded)
            {
                var partnerContributions = partnerContributionsResult.Payload;
                var partnerContributionsCount = partnerContributions.Contributions.Count();
                if (allContributionsModel != null && partnerContributionsCount > 0)
                {
                    allContributionsModel.ContributionsForTable.AddRange(partnerContributions.Contributions);
                    if (skip != null && take != null)
                    {
                        allContributionsModel.TotalCount = allContributionsModel.TotalCount + partnerContributionsCount;
                        List<ContribTableViewModel> allFilteredContributions = new List<ContribTableViewModel>();
                        allFilteredContributions = allContributionsModel.ContributionsForTable.Take(Convert.ToInt32(take)).ToList();
                        allContributionsModel.ContributionsForTable = allFilteredContributions;
                    }
                }
            }

            if (allContributionsModel == null || !allContributionsModel.ContributionsForTable.Any())
            {
                return NotFound();
            }

            return Ok(getContributionResult.Payload);
        }

        // GET: /Contribution/GetArchivedCreatedByUserId/{id}
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpGet("GetArchivedCreatedByUserId/{id}")]
        public async Task<IActionResult> GetArchivedCreatedByUserId(string id)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ErrorInfo { Message = $"{_localizer["User Id is null"]}" });
            }

            var getContributionResult = await _contributionService.GetArchivedCreatedByCohealerAsync(id, AccountId);
            if (!getContributionResult.Succeeded)
            {
                _logger.LogError(
                    $"GetArchivedCreatedByCohealer {id} failed: {getContributionResult.Message}",
                    DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo(getContributionResult.Message));
            }

            var contributions = (List<ContribTableViewModel>)getContributionResult.Payload;
            if (!contributions.Any())
            {
                return NotFound();
            }

            return Ok(getContributionResult.Payload);
        }

        //POST: /Contribution
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] JsonElement contributionJson)
        {
            if (string.IsNullOrEmpty(contributionJson.ToString()))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution model is null"]));
            }

            var contribution = JsonConvert.DeserializeObject<ContributionBaseViewModel>(contributionJson.ToString());

            if (contribution == null)
            {
                return BadRequest(new ErrorInfo("Unable to deserialize contribution. Please check the contribution type"));
            }

            var validationResult = await contribution.ValidateAsync();

            if (validationResult.IsValid)
            {
                var result = await _contributionService.Insert(contribution, AccountId);
                var contributionInserted = (ContributionBaseViewModel)result.Payload;
                if (result.Succeeded)
                {
                    return Created($"Contribution/{contributionInserted.Id}", contributionInserted); //HTTP201 Resource created
                }

                _logger.LogError($"Unable to add contribution: {result.Message} for user User.Id", contribution.UserId, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // PUT: /Contribution/id
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] JsonElement contributionJson)
        {
            if (string.IsNullOrEmpty(contributionJson.ToString()))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution model is null"]));
            }

            var contribution = JsonConvert.DeserializeObject<ContributionBaseViewModel>(contributionJson.ToString());

            if (contribution == null)
            {
                return BadRequest(new ErrorInfo("Unable to deserialize contribution. Please check the contribution type"));
            }

            if (contribution.Id != id)
            {
                return BadRequest(new ErrorInfo { Message = _localizer["Contribution id not match"] });
            }

            var validationResult = await contribution.ValidateAsync();

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _contributionService.Update(contribution, AccountId);
            if (result.Succeeded)
            {
                var updatedContribution = (ContributionBaseViewModel)result.Payload;
                try
                {
                    await _profilePageService.UpdateProfilepageContribution(contribution.UserId, updatedContribution);
                }
                catch
                {

                }

                return Accepted(updatedContribution);
            }

            _logger.LogError($"Unable to update contribution: {result.Message} with Id: ", contribution.Id, DateTime.Now.ToString("F"));
            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        // DELETE: /Contribution/id
        [Authorize(Policy = "IsOwnerOrAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var result = await _contributionService.Delete(id);

                if (result.Succeeded)
                {
                    return NoContent();
                }

                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return BadRequest(new ErrorInfo(_localizer["Contribution Id is null"]));
        }

        // POST: /Contribution/ChangeStatus/{id}
        [Authorize(Roles = "Admin, SuperAdmin")]
        [HttpPost("ChangeStatus/{id}")]
        public async Task<IActionResult> ChangeStatus(string id, [FromBody] AdminReviewNoteViewModel model)
        {
            if (string.IsNullOrWhiteSpace(id) || model == null)
            {
                return BadRequest(new ErrorInfo(_localizer["Review note model is null"]));
            }

            var validationResult = await _reviewNoteValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _contributionService.ChangeStatusAsync(id, AccountId, AccountId, model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok(result.Payload);
        }

        // POST: /Contribution/BookSessionTime
        [Authorize]
        [HttpPost("BookSessionTime")]
        public async Task<IActionResult> BookSessionTime([FromBody] BookSessionTimeViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new ErrorInfo(_localizer["Book course time model is null"]));
            }

            var validationResult = await _selectSessionTimeValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {

                var result = _contributionBookingService.BookSessionTimeAsync(new List<BookSessionTimeViewModel> { model }, AccountId);

                if (result.Succeeded)
                {
                    return Ok((BookSessionTimeViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        [Authorize]
        [HttpPost("UserViewedRecording")]
        public async Task<IActionResult> UserViewedRecording([FromBody] UserViewedRecordingViewModel model)
        {
            await _contributionService.UserViewedRecording(model);

            return Ok();
        }

        // POST: /Contribution/RevokeBookSessionTime
        [Authorize]
        [HttpPost("RevokeBookSessionTime")]
        public async Task<IActionResult> RevokeBookSessionTime([FromBody] BookSessionTimeViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new ErrorInfo(_localizer["Book course time model is null"]));
            }

            var validationResult = await _selectSessionTimeValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {
                var result = await _contributionBookingService.RevokeBookingOfSessionTimeAsync(model, AccountId);

                if (result.Succeeded)
                {
                    return Ok(result.Message);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        // POST: /Contribution/ShareViaEmail
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("ShareViaEmail")]
        public async Task<IActionResult> ShareLinkViaEmail([FromBody] ShareContributionEmailViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new ErrorInfo(_localizer["ShareContributionEmail model is null"]));
            }

            var validationResult = await _shareContributionModelValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {
                var result = await _contributionService.ShareContribution(model, AccountId);

                return Ok(result.Message);
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        // GET: /Contribution/GetForCohealerDashboard
        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetForCohealerDashboard")]
        public async Task<IActionResult> GetForCohealerDashboard()
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var dashboardContributionsVm = await _contributionService.GetDashboardContributionsForCohealerAsync(AccountId);
            var partnerContributionsResult = await _contributionService.GetPartnerContributions(AccountId, null, new List<ContributionStatuses> { ContributionStatuses.Approved }, true);

            var closestSessionsForBanner = new List<ClosestClassForBannerViewModel>();
            if (dashboardContributionsVm.ClosestClassForBanner != null)
            {
                closestSessionsForBanner.Add(dashboardContributionsVm.ClosestClassForBanner);
            }

            if (partnerContributionsResult.Succeeded)
            {
                var parnterContributions = partnerContributionsResult.Payload.Contributions
                    .Select(x => new ContributionOnDashboardViewModel
                    {
                        ClosestSession = x.ClosestSession,
                        Id = x.Id,
                        Title = x.Title,
                        Type = x.Type,
                        ContributionImage = x.PreviewContentUrls?.FirstOrDefault(),
                        UserId = x.UserId,
                        TimeZoneShortForm = x.TimeZoneShortForm,
                    });

                dashboardContributionsVm.ContributionsForDashboard.AddRange(parnterContributions);
                if (partnerContributionsResult.Succeeded
                    && partnerContributionsResult.Payload?.ClosestClassForBanner != null)
                {
                    closestSessionsForBanner.Add(partnerContributionsResult.Payload.ClosestClassForBanner);
                }
            }

            _contributionService.GetActiveCampaignResult(AccountId);


            // log user activity;
            var user = await _userService.GetByAccountIdAsync(AccountId);
            if (user != null)
            {
                await _userService.LogUserActivity(user.Id);
            }

            if (dashboardContributionsVm.ContributionsForDashboard.Any())
            {
                dashboardContributionsVm.ClosestClassForBanner = closestSessionsForBanner
                    .Where(x => x != null)
                    .OrderBy(x => x.StartTime)
                    .FirstOrDefault();

                return Ok(dashboardContributionsVm);
            }

            return NotFound();
        }
        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetAllContributionsDataForCohealer")]
        public async Task<IActionResult> GetAllSessionsForCohealer(int? skip, int? take)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var dashboardContributionsVm = await _contributionService.GetAllSessionsForCohealer(AccountId, false, skip, take);
            var partnerContributionsResult = await _contributionService.GetAllSessionsForCohealer(AccountId, true, skip, take, null, new List<ContributionStatuses> { ContributionStatuses.Approved });

            var closestSessionsForBanner = new List<ClosestClassForBannerViewModel>();
            if (dashboardContributionsVm.ClosestClassForBanner != null)
            {
                closestSessionsForBanner.Add(dashboardContributionsVm.ClosestClassForBanner);
            }

            if (partnerContributionsResult.ContributionsForDashboard.Count() > 0)
            {
                dashboardContributionsVm.ContributionsForDashboard.AddRange(partnerContributionsResult.ContributionsForDashboard);
                if (partnerContributionsResult.ClosestClassForBanner != null)
                {
                    closestSessionsForBanner.Add(partnerContributionsResult.ClosestClassForBanner);
                }
            }
            if (dashboardContributionsVm.ContributionsForDashboard.Any())
            {
                return Ok(dashboardContributionsVm);
            }

            return NotFound();
        }
        // GET: /Contribution/GetForAdmin
        [Authorize(Roles = "Admin, SuperAdmin")]
        [HttpGet("GetForAdmin")]
        public async Task<IActionResult> GetForAdmin()
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var contributionsGroupedVm = await _contributionService.GetAllContributionsForAdminAsync(AccountId);
            if (contributionsGroupedVm != null)
            {
                return Ok(contributionsGroupedVm);
            }

            return NotFound();
        }

        // GET: /Contribution/GetCohealerInfoForClient/{cohealerUserId}
        [Authorize]
        [HttpGet("GetCohealerInfoForClient/{cohealerUserId}")]
        public async Task<IActionResult> GetCohealerInfoForClient(string cohealerUserId)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var cohealerInfo = await _contributionService.GetCohealerInfoForClient(cohealerUserId, AccountId);
            if (cohealerInfo != null)
            {
                return Ok(cohealerInfo);
            }

            return NotFound();
        }

        // POST: /Contribution/SetClassAsCompleted
        [Authorize(Roles = "Cohealer")]
        [HttpPost("SetClassAsCompleted")]
        public async Task<IActionResult> SetClassAsCompleted(SetClassAsCompletedViewModel model)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var validationResult = await _setClassAsCompletedValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {
                var result = await _contributionService.SetContributionClassAsCompleted(model, AccountId);
                if (result.Succeeded)
                {
                    return Ok((ContributionBaseViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        // POST: /Contribution/SetSelfPacedClassAsCompleted
        [Authorize]
        [HttpPost("SetSelfPacedClassAsCompleted")]
        public async Task<IActionResult> SetSelfPacedClassAsCompleted(SetClassAsCompletedViewModel model)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var validationResult = await _setClassAsCompletedValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {
                var result = await _contributionService.SetContributionSelfPacedClassAsCompleted(model, AccountId);
                if (result.Succeeded)
                {
                    return Ok((ContributionBaseViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        // POST: /Contribution/SetAsCompleted
        [Authorize(Roles = "Cohealer")]
        [HttpPost("SetAsCompleted")]
        public async Task<IActionResult> SetAsCompleted(SetAsCompletedViewModel model)
        {
            if (AccountId == null)
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var validationResult = await _setAsCompletedValidator.ValidateAsync(model);

            if (validationResult.IsValid)
            {
                var result = await _contributionService.SetContributionAsCompletedAsync(model, AccountId);
                if (result.Succeeded)
                {
                    return Ok((ContributionBaseViewModel)result.Payload);
                }

                return BadRequest(new ErrorInfo(result.Message));
            }

            return BadRequest(new ErrorInfo(validationResult.ToString()));
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("PartnerContributions")]
        [ProducesResponseType(200, Type = typeof(List<ContribTableViewModel>))]
        public async Task<IActionResult> GetPartnerContributions()
        {
            var contributionsResult = await _contributionService.GetPartnerContributions(AccountId);
            return contributionsResult.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("GetCohealerContributionsTimeRanges")]
        [ProducesResponseType(200, Type = typeof(List<CohealerContributionTimeRangeViewModel>))]
        public async Task<IActionResult> GetCohealerContributionsTimeRanges()
        {
            var cohealersContributionsTimeRanges = await _contributionRootService.GetCohealerContributionsTimeRangesForCohealer(AccountId);
            return Ok(cohealersContributionsTimeRanges);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("RescheduleOneToOneBooking")]
        [ProducesResponseType(200, Type = typeof(ContributionOneToOneViewModel))]
        public async Task<IActionResult> RescheduleOneToOneBooking([FromBody] RescheduleOneToOneRequestModel requestModel)
        {
            var contribution = await _contributionService.RescheduleOneToOneCoachBooking(
                AccountId,
                requestModel.ContributionId,
                requestModel.RescheduleFromId,
                requestModel.RescheduleToId,
                requestModel.Note,
                requestModel.Offset);

            if (contribution.Succeeded)
            {
                return Ok(contribution.Payload);
            }

            if (contribution.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(contribution.Message);
        }

        [Authorize(Roles = "Client")]
        [HttpPost("ClientOneToOneRescheduling")]
        [ProducesResponseType(200, Type = typeof(ContributionOneToOneViewModel))]
        public async Task<IActionResult> ClientOneToOneRescheduling([FromBody] RescheduleOneToOneRequestModel requestModel)
        {
            var contribution = await _contributionService.RescheduleOneToOneClientBooking(
                AccountId,
                requestModel.ContributionId,
                requestModel.RescheduleFromId,
                requestModel.RescheduleToId,
                requestModel.Note,
                requestModel.Offset);

            if (contribution.Succeeded)
            {
                return Ok(contribution.Payload);
            }

            if (contribution.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(contribution.Message);
        }

        /// <summary>
        /// Gets slots for contribution view for coach or admin
        /// </summary>
        /// <param name="contributionId">The contribution identifier.</param>
        /// <param name="request">The request model</param>
        /// <returns></returns>
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        [HttpPost("{contributionId}/GetSlots")]
        public async Task<IActionResult> GetSlots(string contributionId, [FromQuery] int offset = 0, [FromBody] OneToOneSessionDataUi request = default)
        {
            return Ok(await _contributionRootService.GetAvailabilityTimesForCoach(contributionId, offset, request));
        }

        /// <summary>
        /// Gets the slots during contribution creation/editing
        /// </summary>
        /// <param name="schedulingCriteria">The scheduling criteria.</param>
        /// <returns></returns>
        [Authorize(Roles = "Cohealer")]
        [HttpPost("CalculateSlots")]
        public async Task<IActionResult> GetSlots([FromBody] OneToOneSessionDataUi schedulingCriteria)
        {
            return Ok(await _contributionRootService.CalculateSlots(AccountId, schedulingCriteria));
        }

        [HttpPost("{contributionId}/GetClientSlots")]
        public async Task<IActionResult> GetClientSlots(string contributionId, [FromQuery] int offset)
        {
            return Ok(await _contributionRootService.GetAvailabilityTimesForClient(contributionId, AccountId, offset, string.Empty));
        }
        [HttpPost("{contributionId}/GetClientSlotsWithTimezone")]
        public async Task<IActionResult> GetClientSlotsWithTimezone(string contributionId, [FromQuery] int offset, [FromQuery] string timezoneId)
        {
            return Ok(await _contributionRootService.GetAvailabilityTimesForClient(contributionId, AccountId, offset, timezoneId, withTimeZoneId: true));
        }
        [Authorize(Roles = "Cohealer")]
        [HttpPost("Draft")]
        public async Task<IActionResult> PostUnfinished([FromBody] JsonElement contributionJson)
        {
            if (string.IsNullOrEmpty(contributionJson.ToString()))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution model is null"]));
            }

            var contribution = JsonConvert.DeserializeObject<ContributionBaseViewModel>(contributionJson.ToString());

            if (contribution == null)
            {
                return BadRequest(new ErrorInfo("Unable to deserialize contribution. Please check the contribution type"));
            }

            var validationResult = _unfinishedContributionValidator.Validate(contribution);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult);
            }
            var result = await _contributionService.InsertUnfinished(contribution, AccountId);

            if (result.Failed)
            {
                _logger.LogError($"Unable to add contribution: {result.Message} for user User.Id", contribution.UserId, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            var contributionInserted = result.Payload;
            return Created($"Contribution/{contributionInserted.Id}", contributionInserted); //HTTP201 Resource created
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPut("Draft")]
        public async Task<IActionResult> PutUnfinished([FromBody] JsonElement contributionJson)
        {
            if (string.IsNullOrEmpty(contributionJson.ToString()))
            {
                return BadRequest(new ErrorInfo(_localizer["Contribution model is null"]));
            }

            var contribution = JsonConvert.DeserializeObject<ContributionBaseViewModel>(contributionJson.ToString());

            if (contribution == null)
            {
                return BadRequest(new ErrorInfo("Unable to deserialize contribution. Please check the contribution type"));
            }

            var validationResult = _unfinishedContributionValidator.Validate(contribution);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult);
            }
            contribution.DefaultCurrency = string.IsNullOrWhiteSpace(contribution.DefaultCurrency) ? "usd" : contribution.DefaultCurrency;
            contribution.DefaultSymbol = string.IsNullOrWhiteSpace(contribution.DefaultSymbol) ? "$" : contribution.DefaultSymbol;
            var result = await _contributionService.UpdateUnfinished(contribution, AccountId);

            if (result.Failed)
            {
                _logger.LogError($"Unable to update contribution: {result.Message} for user User.Id", contribution.UserId, DateTime.Now.ToString("F"));
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok(result.Payload);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpDelete("Draft/{contributionId}")]
        public async Task<IActionResult> DeleteUnfinished([FromRoute] string contributionId)
        {
            var removeRequestResult = await _contributionService.DeleteUnfinished(contributionId, AccountId);

            return removeRequestResult.ToActionResult();
        }
        [Authorize(Roles = "Cohealer")]
        [HttpDelete("DeleteContribById/{id}")]
        public async Task<IActionResult> DeleteContribution(string id)
        {
            var removeRequestResult = await _contributionService.DeleteContribution(id, AccountId);

            return removeRequestResult.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("Draft/{contributionId}/Submit")]
        public async Task<IActionResult> SubmitUnfinished([FromRoute] string contributionId)
        {
            var submitResult = await _contributionService.SubmitUnfinished(contributionId, AccountId);

            if (submitResult.Failed)
            {
                return BadRequest(new ErrorInfo { Message = submitResult.Message });
            }

            return Ok(submitResult.Payload);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("Draft")]
        public async Task<IActionResult> GetLastUnfinished()
        {
            var lastContribution = await _contributionService.GetLastUnfinishedAsync(AccountId);

            return new ObjectResult(lastContribution.Payload);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("{contributionId}/UseAsTemplate")]
        public async Task<IActionResult> UseAsTemplate([FromRoute] string contributionId)
        {
            var useAsTemplateResult = await _contributionService.UseAsTemplate(contributionId, AccountId);

            return useAsTemplateResult.ToActionResult();
        }


        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddProfileContribution")]
        public async Task<IActionResult> AddProfileContribution([FromBody] ProfilePageViewModel pcViewModel)
        {
            if (pcViewModel == null)
            {
                return BadRequest();
            }
            var validationResult = await _profilePageValidator.ValidateAsync(pcViewModel);
            if (validationResult.IsValid)
            {
                if (!string.IsNullOrEmpty(pcViewModel.UserId))
                {
                    var profileConrtibution = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(x => x.UserId == pcViewModel.UserId);
                    if (profileConrtibution == null)
                    {
                        var result = await _profilePageService.Insert(pcViewModel, AccountId);
                        if (result.Failed)
                        {
                            _logger.LogError($"Unable to add profile contribution: {result.Message} for user User.Id", pcViewModel.UserId, DateTime.Now.ToString("F"));
                            return BadRequest(new ErrorInfo { Message = result.Message });
                        }
                        return Ok(result.Payload);
                    }
                    else
                    {
                        var result = await _profilePageService.Update(profileConrtibution.Id, pcViewModel, AccountId);
                        if (result.Failed)
                        {
                            _logger.LogError($"Unable to update profile contribution: {result.Message} for user User.Id", pcViewModel.UserId, DateTime.Now.ToString("F"));
                            return BadRequest(new ErrorInfo { Message = result.Message });
                        }
                        return Ok(result.Payload);
                    }
                }
            }
            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }
        [Authorize]
        [HttpPost("GetProfileContribution")]
        public async Task<IActionResult> GetProfileContribution()
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var getProfileContribution = await _profilePageService.GetProfilePage(AccountId);
                if (getProfileContribution != null)
                {
                    return Ok(getProfileContribution);
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
        [HttpPost("save-signoff-data")]
        public async Task<IActionResult> SaveSignoffInfo([FromQuery] string contributionId, [FromQuery] string ipAddress, IFormFile file)
        {

            if (file is null || file.Length <= 0)
            {

                return BadRequest("File is null or empty");
            }
            if (!file.ContentType.Contains("image"))
            {
                return BadRequest("Provide image");
            }
            SignoffInfoViewModel model = new SignoffInfoViewModel();
            model.IPAddress = ipAddress;
            model.ContributionId = contributionId;
            model.TimeStamp = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(model.ContributionId) && !string.IsNullOrEmpty(AccountId) && !string.IsNullOrEmpty(model.IPAddress))
            {
                var message = await _contributionService.SaveSignoffInfo(model, file, AccountId);
                if (message.Succeeded)
                {
                    return Ok("Signoff data succesfully saved.");
                }
            }
            return BadRequest("Error Saving Data.");

        }


        [Authorize(Roles = "Cohealer")]
        [HttpPost("AddOrUpdateCustomTemplate")]
        public async Task<IActionResult> AddOrUpdateCustomTemplate([FromBody] EmailTemplatesViewModel emailTemplates)
        {
            if (emailTemplates == null)
            {
                return BadRequest();
            }
            if (String.IsNullOrEmpty(AccountId))
            {
                var errorMessage = $"{AccountId} should not be null or empty";
                _logger.LogError(errorMessage);
                return Unauthorized(errorMessage);
            }
            var validationResult = await _emailtemplateValidator.ValidateAsync(emailTemplates);
            if (validationResult.IsValid)
            {
                var result = await _contributionService.AddOrUpdateCustomTemplate(emailTemplates, AccountId);
                if (result.Succeeded)
                {
                    return Ok(result.Message);
                }
                else
                {
                    return BadRequest(result.Message);
                }

            }
            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }
        [Authorize]
        [HttpGet("GetCustomTemplateByContributionId/{contributionId}")]
        public async Task<IActionResult> GetCustomTemplateByContributionId([FromRoute] string contributionId)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var getCustomTemplate = await _contributionService.GetCustomTemplateByContributionId(contributionId);
                if (getCustomTemplate != null)
                {
                    return Ok(getCustomTemplate);
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
        [HttpPut("EnableEmailTemplate")]
        public async Task<IActionResult> EnableEmailTemplate([FromQuery] string contributionId, [FromQuery] string emailType, bool IsEnabled)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                if (String.IsNullOrEmpty(contributionId))
                {
                    var errorMessage = $"{contributionId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID can't be empty.");
                }
                var result = await _contributionService.EnableEmailTemplate(AccountId, contributionId, emailType, IsEnabled);
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
        [HttpPut("UpdateEmailTemplate")]
        public async Task<IActionResult> UpdateEmailTemplate([FromBody] CustomTemplate customTemplate)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                if (String.IsNullOrEmpty(customTemplate.ContributionId))
                {
                    var errorMessage = $"{customTemplate.ContributionId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID can't be empty.");
                }
                var result = await _contributionService.UpdateEmailTemplate(customTemplate.ContributionId, customTemplate);
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

        [Authorize]
        [HttpGet("GetCoachContributionsForZapier")]
        public async Task<IActionResult> GetCoachContributionsForZapier()
        {

            var model = await _contributionService.GetCoachContributionsForZapier(AccountId);
            if (model == null)
            {
                return BadRequest();
            }
            return Ok(model.Payload);

        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("DownloadSelfpacedModuleDetails")]
        public async Task<IActionResult> DownloadSelfpacedModuleDetails([FromQuery] string contributionId)
        {
            if (String.IsNullOrEmpty(AccountId))
            {
                var errorMessage = $"{AccountId} should not be null or empty";
                _logger.LogError(errorMessage);
                return Unauthorized(errorMessage);
            }
            if (String.IsNullOrEmpty(contributionId))
            {
                var errorMessage = $"{contributionId} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest("Contribution Id can't be empty.");
            }

            try
            {
                var detail = await _contributionService.DownloadSelfpacedModuleDetails(contributionId, AccountId);

                using (var memoryStream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(memoryStream))
                    {
                        using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
                        {
                            //CSV Headers
                            csvWriter.WriteField("First Name");
                            csvWriter.WriteField("Last Name");
                            csvWriter.WriteField("Email");
                            detail.ToList()[0].ModuleContentList.ForEach(f =>
                            {
                                csvWriter.WriteField($"{f.ModuleName}, {f.ContentName}");
                            });
                            csvWriter.NextRecord();

                            //CSV Records
                            detail.ToList().ForEach(d =>
                            {
                                //csv client Info
                                csvWriter.WriteField(d.FirstName);
                                csvWriter.WriteField(d.LastName);
                                csvWriter.WriteField(d.Email);
                                //session Details in columns
                                d.ModuleContentList.ForEach(f =>
                                {
                                    csvWriter.WriteField(f.Status);
                                });
                                csvWriter.NextRecord();
                            });
                        }
                    }
                    return File(memoryStream.ToArray(), "text/csv", "Client Self-Paced Engagement Data.csv");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpGet("GetCustomizedContributions")]
        public async Task<IActionResult> GetCustomizedContributions([FromQuery] string ContributionId)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }

                var result = await _contributionService.GetCustomizedContributions(AccountId, ContributionId);
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
        [Authorize]
        [HttpPut("CopyContributionEmailSettings")]
        public async Task<IActionResult> CopyContributionEmailSettings([FromQuery] string FromContributionId, [FromQuery] string ToContributionId)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                if (String.IsNullOrEmpty(FromContributionId))
                {
                    var errorMessage = $"{FromContributionId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID can't be empty.");
                }
                if (String.IsNullOrEmpty(ToContributionId))
                {
                    var errorMessage = $"{ToContributionId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID can't be empty.");
                }
                var result = await _contributionService.CopyContributionEmailSettings(FromContributionId, ToContributionId);
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

        [Authorize]
        [HttpPut("EnableBrandingColorsOnEmailTemplates")]
        public async Task<IActionResult> EnableBrandingColorsOnEmailTemplates([FromQuery] string contributionId, bool IsEnabled)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                if (String.IsNullOrEmpty(contributionId))
                {
                    var errorMessage = $"{contributionId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID can't be empty.");
                }
                var result = await _contributionService.EnableBrandingColorsOnEmailTemplates(AccountId, contributionId, IsEnabled);
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
        [HttpPost("SendTestCustomEmailNotification")]
        public async Task<IActionResult> SendTestCustomEmailNotification([FromBody] CustomTemplate customTemplate)
        {
            try
            {
                if (String.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                if (String.IsNullOrEmpty(customTemplate.ContributionId))
                {
                    var errorMessage = $"Contribution ID should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("Contribution ID should not be null or empty");
                }
                if (String.IsNullOrEmpty(customTemplate.EmailSubject))
                {
                    var errorMessage = $"EmailSubject should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("EmailSubject should not be null or empty");
                }
                if (String.IsNullOrEmpty(customTemplate.EmailText))
                {
                    var errorMessage = $"EmailText should not be null or empty";
                    _logger.LogError(errorMessage);
                    return BadRequest("EmailText should not be null or empty");
                }
                await _notificationService.SendTestEmailNotification(AccountId,customTemplate);

                 return Ok();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }
    }
}
