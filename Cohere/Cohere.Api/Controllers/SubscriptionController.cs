using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Domain.Service.Abstractions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SubscriptionController : CohereController
    {
        private readonly IContributionService _contributionService;

        public SubscriptionController(IContributionService contributionService)
        {
            _contributionService = contributionService;
        }

        [HttpGet("ListIncompleteSubscriptions")]
        [Authorize(Roles = "Cohealer")]
        public async Task<IActionResult> ListIncompleteSubscriptions()
        {
            var result = await _contributionService.ListCoachIncompleteSubscriptions(AccountId);

            return Ok(result);
        }

        [HttpGet("ListClientIncompleteSubscriptions")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ListClientIncompleteSubscriptions()
        {
            var result = await _contributionService.ListClientIncompleteSubscription(AccountId);

            return Ok(result);
        }

        [HttpPost("CreatePaidTierOptionCheckoutSession")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CreateCheckoutSession()
        {
            var result = await _contributionService.CreateCheckoutSession(AccountId);

            if (result.Failed)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Payload);
        }
    }
}