using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Domain.Service.Abstractions;

using CsvHelper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("api/cohealer-income")]
    [ApiController]
    public class CohealerIncomeController : CohereController
    {
        private readonly ICohealerIncomeService _cohealerIncomeService;

        public CohealerIncomeController(ICohealerIncomeService cohealerIncomeService)
        {
            _cohealerIncomeService = cohealerIncomeService;
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardUserIncome()
        {
            var income = await _cohealerIncomeService.GetDashboardIncomeAsync(AccountId);

            if (!income.Any())
            {
                return NotFound();
            }

            return Ok(income);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("total")]
        public async Task<IActionResult> GetTotalUserIncome()
        {
            var income = await _cohealerIncomeService.GetTotalIncomeAsync(AccountId);

            if (income == null)
            {
                return BadRequest();
            }

            return Ok(income);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("sales-history")]
        public async Task<IActionResult> GetUserSalesHistory()
        {
            var contributionSales = await _cohealerIncomeService.GetContributionSalesAsync(AccountId);

            if (contributionSales == null)
            {
                return BadRequest();
            }

            await using var memoryStream = new MemoryStream();
            await using (var writer = new StreamWriter(memoryStream))
            await using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csvWriter.WriteRecordsAsync(contributionSales);
            }

            return File(memoryStream.ToArray(), "text/csv", "Cohere financial activity.csv");
        }
    }
}