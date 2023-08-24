using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountPreferencesController : CohereController
    {
        private readonly IAccountService<AccountViewModel, Account> _accountService;

        public AccountPreferencesController(IAccountService<AccountViewModel, Account> accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var account = await _accountService.GetOne(AccountId);

            return Ok(account.AccountPreferences);
        }

        [HttpPost]
        public async Task<IActionResult> Post(AccountPreferencesViewModel model)
        {
            var result = await _accountService.SetUserPreferences(AccountId, model);

            return result.ToActionResult();
        }
    }
}