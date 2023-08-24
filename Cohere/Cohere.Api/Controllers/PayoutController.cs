using System.Threading.Tasks;

using Cohere.Api.Utils;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PayoutController : CohereController
    {
        private readonly IPayoutService _payoutService;
        private readonly IValidator<GetPaidViewModel> _getPaidValidator;

        public PayoutController(IPayoutService payoutService, IValidator<GetPaidViewModel> getPaidValidator)
        {
            _payoutService = payoutService;
            _getPaidValidator = getPaidValidator;
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("get-paid")]
        public async Task<IActionResult> GetPaid([FromBody] GetPaidViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _getPaidValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var payoutResult = await _payoutService.GetPaidAsync(AccountId, model.Amount, _payoutService.Currency, model.IsStandardAccount);

            if (!payoutResult.Succeeded)
            {
                if (payoutResult.Message.Equals("Unable to get paid. Add your bank account information"))
                    return BadRequest(new ErrorInfo(payoutResult.Message, "payouts_not_allowed"));
                return BadRequest(new ErrorInfo(payoutResult.Message));
            }

            return Ok(payoutResult.Payload);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("get-paid/full")]
        public async Task<IActionResult> GetPaid([FromQuery] bool isStandardAccount)
        {
            var payoutResult = await _payoutService.GetPaidAsync(AccountId, isStandardAccount);

            if (!payoutResult.Succeeded)
            {
                if(payoutResult.Message.Equals("Unable to get paid. Add your bank account information"))
                    return BadRequest(new ErrorInfo(payoutResult.Message, "payouts_not_allowed"));
                return BadRequest(new ErrorInfo(payoutResult.Message));
            }

            return Ok(payoutResult.Payload);
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var payoutResult = await _payoutService.GetAvailableBalanceAsync(AccountId);

            if (!payoutResult.Succeeded)
            {
                return BadRequest(new ErrorInfo(payoutResult.Message));
            }

            return Ok(payoutResult.Payload);
        }
    }
}
