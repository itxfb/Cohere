using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.SimpleEmail.Model;
using Amazon.SQS.Model;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Workers;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class PaidTierController : CohereController
    {
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;
       
        public PaidTierController(IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService)
        {
            _paidTiersService = paidTiersService;
        }

        // GET: /PaidTier
        [HttpGet]
        public Task<IEnumerable<PaidTierOptionViewModel>> GetAll()
        {           
            if (string.IsNullOrWhiteSpace(AccountId))
            {
                return null;
            }
            return _paidTiersService.GetAll(AccountId);
        }

        // GET: /PaidTier/paidTierId
        [HttpGet("{id}")]
        public Task<PaidTierOptionViewModel> Get(string id)
        {
            return _paidTiersService.GetOne(id);
        }

        // POST: /PaidTier
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("create/option")]
        public async Task<IActionResult> CreatePaidTier(PaidTierOptionViewModel viewModel)
        {
           
            if (viewModel.Default)
            {
                return (await _paidTiersService.Insert(viewModel)).ToActionResult();
            }

            return (await _paidTiersService.CreatePaidTierOptionProductPlan(viewModel,"usd")).ToActionResult();
        }

        // POST: /create-checkout-session
        [Authorize(Roles = "Cohealer")]
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreatePaidTierOptionCheckoutSession(
            CreatePaidTierCheckoutSessionModel model)
        {
            if (!Enum.TryParse<PaidTierOptionPeriods>(model.PaidTierPeriod, out var paymentPeriod))
            {
                return BadRequest();
            }

            return (await _paidTiersService.CreateCheckoutSessionSubscription(
                    model.PaidTierId,
                    paymentPeriod,
                    model.ClientAccountId))
                .ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("cancel/paidTier")]
        public async Task<IActionResult> CancelPaidTierPlan([FromBody] CancelPaidTierPlanModel model)
        {
            var result = await _paidTiersService.CancelPaidTierPlan(AccountId);
            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("upgrade/paidTier")]
        public async Task<IActionResult> UpgradePaidTierPlan([FromBody] UpgradePaidTierPlanModel model)
        {
            if (!Enum.TryParse<PaidTierOptionPeriods>(model.PaymentOption, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result = await _paidTiersService.UpgradePaidTierPlan(AccountId, model.PaidTierId, paymentOptionsEnum);

            return result.ToActionResult();
        }

        // GET: /getCurrent/accountId
        [Authorize(Roles = "Cohealer")]
        [HttpGet("getCurrent")]
        public Task<CurrentPaidTierViewModel> GetCurrentPaidTier()
        {
            if (string.IsNullOrWhiteSpace(AccountId))
            {
                return null;               
            }
            return _paidTiersService.GetCurrentPaidTierViewModel(AccountId);
        }
    }
}