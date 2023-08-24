using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("[controller]")]
    [ApiController]
    public class AffiliateController : CohereController
    {
        private readonly IAffiliateService _affiliateService;
        private readonly IAffiliateCommissionService _affiliateCommissionService;
        private readonly IValidator<InviteEmailsRequestModel> _inviteEmailsValidator;
        private readonly IValidator<GetPaidViewModel> _getPaidValidator;

        public AffiliateController(
            IAffiliateService affiliateService,
            IAffiliateCommissionService affiliateCommissionService,
            IValidator<InviteEmailsRequestModel> inviteEmailsValidator,
            IValidator<GetPaidViewModel> getPaidValidator)
        {
            _affiliateService = affiliateService;
            _affiliateCommissionService = affiliateCommissionService;
            _inviteEmailsValidator = inviteEmailsValidator;
            _getPaidValidator = getPaidValidator;
        }

        [HttpGet("GetAffiliateName")]
        public async Task<IActionResult> GetAffiliateNameByInviteCode([FromQuery] string inviteCode)
        {
            var userNameRequest = await _affiliateService.GetUserNameByInviteCode(inviteCode);

            if (userNameRequest.Failed)
            {
                return BadRequest(userNameRequest.Message);
            }

            if (userNameRequest.Payload is null)
            {
                return NotFound();
            }

            return userNameRequest.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("InviteByEmail")]
        public async Task<IActionResult> InviteByEmail([FromBody] InviteEmailsRequestModel model)
        {
            if (model == null)
            {
                return BadRequest("model should not be null");
            }

            var validationResult = await _inviteEmailsValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _affiliateService.ShareReferralLink(model.EmailAddresses, AccountId);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("IsEnrolled")]
        public async Task<IActionResult> GetCurrentEnrollmentStatus()
        {
            return Ok(await _affiliateService.IsEnrolled(AccountId));
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("ToggleEnrollmentStatus")]
        public async Task<IActionResult> ToggleEnrollmentStatus()
        {
            var result = await _affiliateService.ToggleEnrollmentStatus(AccountId);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("AffiliateRevenueSummary")]
        public async Task<IActionResult> GetAffiliateRevenueSummary()
        {
            var result = await _affiliateCommissionService.GetAffiliateRevenueSummaryAsync(AccountId);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("GetPaid")]
        public async Task<IActionResult> GetPaid([FromBody] GetPaidViewModel model)
        {
            var validationResult = await _getPaidValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
            }

            var result = await _affiliateService.GetPayout(AccountId, model.Amount);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("GetPaid/Full")]
        public async Task<IActionResult> GetFullPaid()
        {
            var result = await _affiliateService.GetFullPayout(AccountId);

            return result.ToActionResult();
        }
    }
}