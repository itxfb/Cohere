using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Service.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ContributionAccessController : CohereController
    {
        private readonly IContributionAccessService _contributionAccessService;

        public ContributionAccessController(IContributionAccessService contributionAccessService)
        {
            _contributionAccessService = contributionAccessService;
        }
        
        [Authorize(Roles = "Cohealer")]
        [HttpPost("{contributionId}/CreateAccessCode")]
        public async Task<IActionResult> CreateAccessCode([FromRoute]string contributionId, [FromQuery]int validPeriodInYears = 10)
        {
            var createAccessCodeResult =
                await _contributionAccessService.CreateAccessCode(contributionId, AccountId, validPeriodInYears);

            return createAccessCodeResult.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("{contributionId}/join")]
        public async Task<IActionResult> Join([FromRoute]string contributionId, [FromBody]ContributionAccessModel model)
        {
            var joinResult =
                await _contributionAccessService.GrantAccessByAccessCode(AccountId, contributionId, model.AccessCode);

            return joinResult.ToActionResult();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpDelete("{contributionId}/{participantId}")]
        public async Task<IActionResult> Delete([FromRoute] string contributionId, [FromRoute] string participantId)
        {
            var deleteResult = await _contributionAccessService.CancelAccess(AccountId, contributionId, participantId);
            return deleteResult.ToActionResult();
        }
    }

    public class ContributionAccessModel
    {
        public string AccessCode { get; set; }
    }
}