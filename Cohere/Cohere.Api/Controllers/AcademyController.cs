using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AcademyController : CohereController
    {
        private readonly IAcademyService _academyService;

        public AcademyController(IAcademyService academyService)
        {
            _academyService = academyService;
        }

        [HttpGet("get-academy-contribution-preview")]
        public async Task<IActionResult> GetAcademyContributionsPreview()
        {
            var result = await _academyService.GetContributionBundledWithPaidTierProductAsync();

            return result.ToActionResult();
        }
    }
}