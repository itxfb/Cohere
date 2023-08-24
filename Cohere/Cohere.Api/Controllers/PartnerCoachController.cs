using System.Linq;
using System.Threading.Tasks;

using Cohere.Api.Settings;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.PartnerCoach;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums;
using Cohere.Entity.Infrastructure.Options;
using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class PartnerCoachController : CohereController
    {
        private readonly IContributionRootService _contributionRootService;
        private readonly IContributionService _contributionService;
        private readonly INotificationService _notificationService;
        private readonly ClientUrlsSettings _urlSettings;
        private readonly IUserService<UserViewModel, User> _userService;
        private readonly IValidator<InvitePartnerCoachViewModel> _invitePartnerValidator;

        public PartnerCoachController(
            IContributionRootService contributionRootService,
            IContributionService contributionService,
            INotificationService notificationService,
            IOptions<ClientUrlsSettings> ulrOptions,
            IUserService<UserViewModel, User> userService,
            IValidator<InvitePartnerCoachViewModel> invitePartnerValidator)
        {
            _contributionRootService = contributionRootService;
            _contributionService = contributionService;
            _notificationService = notificationService;
            _urlSettings = ulrOptions.Value;
            _userService = userService;
            _invitePartnerValidator = invitePartnerValidator;
        }

        [HttpPost("Invite")]
        [Authorize(Roles = nameof(Roles.Cohealer))]
        public async Task<IActionResult> Invite(InvitePartnerCoachViewModel model)
        {
            var validationResult = await _invitePartnerValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                return BadRequest(string.Join("; ", validationResult.Errors.Select(x => x.ErrorMessage)));
            }

            var ownerUser = await _userService.GetByAccountIdAsync(AccountId);

            if (ownerUser == null)
            {
                return BadRequest("User not found");
            }

            foreach (var email in model.Emails)
            {
                var assignCodeOperationResult = await _contributionService.CreatePartnerCoachAssignRequest(model.ContributionId, ownerUser.Id, email);

                if (!assignCodeOperationResult.Succeeded)
                {
                    return assignCodeOperationResult.ToActionResult();
                }

                var assignCode = assignCodeOperationResult.Payload;
                var actionPath = $"PartnerCoach/Assign/{model.ContributionId}/{ownerUser.Id}/{assignCode}";
                var url = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/{actionPath}";

                var contribution = await _contributionRootService.GetOne(model.ContributionId);
                await _notificationService.SendEmailPartnerCoachInvite(email, contribution, url);
            }

            return Ok();
        }

        [HttpGet("Assign/{contributionId}/{contributionOwnerUserId}/{assignCode}")]
        public async Task<IActionResult> Assign([FromRoute] string contributionId, [FromRoute] string contributionOwnerUserId, [FromRoute] string assignCode)
        {
            var contributionViewPath = _urlSettings.ContributionView;
            var dashboardPath = _urlSettings.DashboardPath;
            var siteUrl = _urlSettings.WebAppUrl;
            var url = siteUrl + contributionViewPath;
            var result = await _contributionService.AssignPartnerCoachToContribution(contributionId, contributionOwnerUserId, assignCode);
            if (result.Succeeded)
            {
                url += $"{result.Payload.Id}/about?success=true&contributionName={result.Payload.Title}&isPurchased=true";
            }
            else
            {
                if (result.Payload == null)
                {
                    url = siteUrl + dashboardPath + "?success=false";
                }
                else
                {
                    url = siteUrl + contributionViewPath + result.Payload.Id + $"/about?success=false&contributionName={result.Payload.Title}";
                }
            }

            return Redirect(url);
        }

        [HttpDelete("{contributionId}/{userId}")]
        [Authorize(Roles = "Cohealer, Admin, SuperAdmin")]
        public async Task<IActionResult> Delete([FromRoute] string contributionId, [FromRoute] string userId)
        {
            var operationResult = await _contributionService.DeletePartnerFromContribution(contributionId, userId, AccountId);

            return operationResult.ToActionResult();
        }

        // Get: /PartnerCoach/ContributionPartners/{contributionId}
        [HttpGet("ContributionPartners/{contributionId}")]
        public async Task<IActionResult> GetContributionPartnersList(string contributionId)
        {
            var result = await _contributionService.GetContributionPartnersAsync(contributionId);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo(result.Message));
        }
    }
}
