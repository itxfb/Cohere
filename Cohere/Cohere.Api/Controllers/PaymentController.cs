using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Domain;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment.Plaid;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : CohereController
    {
        private readonly PlaidService _plaidService;
        private readonly IStripeService _stripePaymentService;
        private readonly StripeAccountService _stripeAccountService;
        private readonly IContributionService _contributionService;
        private readonly AccountService _accountService;
        private readonly IUserService<UserViewModel, User> _userService;
        private readonly ContributionPurchaseService _contributionPurchaseService;
        private readonly StripeEventHandler _stripeEventHandler;
        private readonly ILogger<PaymentController> _logger;



        public PaymentController(
            PlaidService plaidService,
            IStripeService stripePaymentService,
            StripeAccountService stripeAccountService,
            IUserService<UserViewModel, User> userService,
            StripeEventHandler stripeEventHandler,
            ILogger<PaymentController> logger,
            ContributionPurchaseService contributionPurchaseService,
            AccountService accountService,
            IContributionService contributionService)
        {
            _plaidService = plaidService;
            _stripePaymentService = stripePaymentService;
            _stripeAccountService = stripeAccountService;
            _userService = userService;
            _accountService = accountService;
            _stripeEventHandler = stripeEventHandler;
            _logger = logger;
            _contributionPurchaseService = contributionPurchaseService;
            _contributionService = contributionService;
        }

        [Authorize(Roles = "Client")]
        [HttpGet("stripe-key")]
        public IActionResult GetStripePublishableKey()
        {
            return Ok(new { PublishableKey = _stripePaymentService.StripePublishableKey });
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("plaid-key")]
        public IActionResult GetPlaidPublicKey()
        {
            var publicKey = _plaidService.GetPublicKey();
            return Ok(new { PublicKey = publicKey });
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangePlaidPublicToken([FromBody] ExchangeTokenViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PublicToken))
            {
                return BadRequest();
            }

            var accessToken = await _plaidService.ExchangePublicTokenAsync(model.PublicToken);
            return Ok(new { AccessToken = accessToken });
        }

        //[Authorize(Roles = "Cohealer")]
        [HttpPost("fetch-stripe-token")]
        public async Task<IActionResult> FetchStripeToken([FromBody] FetchStripeTokenViewModel model)
        {
            if (model == null
                || string.IsNullOrWhiteSpace(model.AccessToken)
                || string.IsNullOrWhiteSpace(model.AccountId))
            {
                return BadRequest();
            }

            var stripeToken = await _plaidService.FetchStripeTokenAsync(model.AccessToken, model.AccountId);
            return Ok(new { StripeToken = stripeToken });
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("attach-external-account")]
        public async Task<IActionResult> AttachExternalAccount([FromBody] AttachExternalAccountViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.StripeToken))
            {
                return BadRequest();
            }

            var userViewModel = await _userService.GetByAccountIdAsync(AccountId);

            var connectedStripeAccountId = userViewModel.ConnectedStripeAccountId;

            if (connectedStripeAccountId == null)
            {
                _logger.LogError($"Coach with accountId {AccountId} has no Connected Stripe Account");
                return BadRequest(new ErrorInfo { Message = "Stripe account is not found." });
            }

            var result = await _stripeAccountService.AddExternalAccountAsync(connectedStripeAccountId, model.StripeToken);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("remove-bank-account")]
        public async Task<IActionResult> RemoveAtachedAccountAsync([FromBody]BankAccountOperationModel model)
        {
            await _stripeAccountService.RemoveBankAccount(AccountId, model.BankAccountId);
            return Ok();
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("set-bank-account-as-default")]
        public async Task<IActionResult> SetAttachedAccountAsDefaultAsync([FromBody]BankAccountOperationModel model)
        {
            await _stripeAccountService.SetBankAccountAsDefault(AccountId, model.BankAccountId);
            return Ok();
        }

        public class BankAccountOperationModel
        {
            public string BankAccountId { get; set; }
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("generate-account-verification-link")]
        public async Task<IActionResult> GenerateAccountVerificationLink(bool forStandardAccount = false)
        {
            var userViewModel = await _userService.GetByAccountIdAsync(AccountId);
            var connectedStripeAccountId = userViewModel.ConnectedStripeAccountId;

            if (!forStandardAccount && connectedStripeAccountId == null)
            {
                _logger.LogError($"Coach with accountId {AccountId} has no Connected Stripe Account");
                return BadRequest(new ErrorInfo { Message = "Stripe account is not found." });
            }
            
            if(forStandardAccount && string.IsNullOrEmpty(userViewModel.StripeStandardAccountId))
            {
                _logger.LogError($"Coach with accountId {AccountId} has no Standard Stripe Account");
                return BadRequest(new ErrorInfo { Message = "Stripe account is not found." });
            }

            var result = await _stripeAccountService.GenerateAccountVerificationLinkAsync(connectedStripeAccountId, userViewModel.StripeStandardAccountId, forStandardAccount);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok(new { Link = result.Payload });
        }

        [Authorize(Roles = "Cohealer")]
        [HttpPost("generate-account-onboarding-link")]
        public async Task<IActionResult> GenerateAccountOnboardingLinkAsync()
        {
            var userViewModel = await _userService.GetByAccountIdAsync(AccountId);
            var connectedStripeAccountId = userViewModel.ConnectedStripeAccountId;

            if (connectedStripeAccountId == null)
            {
                _logger.LogError($"Coach with accountId {AccountId} has no Connected Stripe Account");
                return BadRequest(new ErrorInfo { Message = "Stripe account is not found." });
            }

            var result = await _stripeAccountService.GenerateAccountOnboardingLinkAsync(connectedStripeAccountId);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok(new { Link = result.Payload });
        }

        [Authorize(Roles = "Client")]
        [HttpPost("attach-customer-payment-method")]
        public async Task<IActionResult> AttachCustomerPaymentMethod([FromBody] AttachPaymentMethodViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PaymentMethodId) || string.IsNullOrWhiteSpace(model.ContributionId))
            {
                return BadRequest();
            }

            ContributionBase contribution = null;
            User coachUser = null;
            var userVm = await _userService.GetUserAsync(AccountId);
           
            var account = await _userService.GetAccountAsync(AccountId);
            contribution = await _contributionService.Get(model.ContributionId);
            coachUser = _userService.GetUserWithUserId(contribution.UserId).Result;
            await _contributionPurchaseService.CreateNewStripeCustomerWithSameCurrency(userVm, account, contribution);
            
            var customerStripeAccountId = userVm.CustomerStripeAccountId;
            var standardAccountId = string.Empty;
            if (contribution.PaymentType == Entity.Enums.Contribution.PaymentTypes.Advance && coachUser.IsStandardAccount) standardAccountId = coachUser.StripeStandardAccountId;
            var result = await _stripeAccountService.AttachCustomerPaymentMethodAsync(customerStripeAccountId, model.PaymentMethodId, standardAccountId);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("attach-customer-payment-method-token")]
        public async Task<IActionResult> AttachCustomerPaymentMethod([FromBody] AttachPaymentMethodTokenViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.CardToken) || string.IsNullOrWhiteSpace(model.ContributionId))
            {
                return BadRequest();
            }

            ContributionBase contribution = null;
            User coachUser = null;
            var userVm = await _userService.GetUserAsync(AccountId);

            var account = await _userService.GetAccountAsync(AccountId);
            contribution = await _contributionService.Get(model.ContributionId);
            coachUser = _userService.GetUserWithUserId(contribution.UserId).Result;
            await _contributionPurchaseService.CreateNewStripeCustomerWithSameCurrency(userVm, account, contribution);

           
            var customerStripeAccountId = userVm.CustomerStripeAccountId;
            var standardAccountId = string.Empty;
            if (contribution.PaymentType == Entity.Enums.Contribution.PaymentTypes.Advance && coachUser.IsStandardAccount) standardAccountId = coachUser.StripeStandardAccountId;

            var result = await _stripeAccountService.AttachCustomerPaymentMethodByCardTokenAsync(customerStripeAccountId, model.CardToken, standardAccountId);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok(new { PaymentMethodId = result.Payload });
        }

        [Authorize(Roles = "Client")]
        [HttpPost("CreateCustomerPortalLink")]
        public async Task<IActionResult> CreateCustomerPortalLink()
        {
            var userVm = await _userService.GetByAccountIdAsync(AccountId);

            var customerStripeAccountId = userVm.CustomerStripeAccountId;

            var result = await _stripePaymentService.CreateCustomerPortalLink(customerStripeAccountId);

            if (result.Failed)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Payload);
        }


        [Authorize(Roles = "Client")]
        [HttpPost("CreateCustomerPortalLinkWithId")]
        public async Task<IActionResult> CreateCustomerPortalLinkWithId(string customerId)
        {
            var result = await _stripePaymentService.CreateCustomerPortalLink(customerId);

            if (result.Failed)
            {
                return BadRequest(result.Message);
            }

            return Ok(result.Payload);
        }

        [Authorize(Roles = "Client")]
        [HttpPost("GetAllStripeCustomerIds")]
        public async Task<IActionResult> GetAllStripeCustomer()
        {
            try
            {
                var account = await _userService.GetAccountAsync(AccountId);
                var customerList = _stripeAccountService.GetCustomerAccountList(account.Email);
                return Ok((List<StripeCustomerAccount>)customerList.Payload);
            }
            catch (System.Exception)
            {
                return BadRequest("Invalid Request");
            }  
        }

        [HttpGet("successful-verification")]
        public IActionResult SuccessfulVerification()
        {
            return Ok(new { Status = "Success" });
        }

        [HttpGet("failed-verification")]
        public IActionResult FailedVerification()
        {
            return Ok(new { Status = "Failed" });
        }

        [Authorize(Roles = "Cohealer")]
        [HttpGet("list-bank-accounts")]
        public async Task<IActionResult> ListBankAccounts()
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find Name Identifier in JWT token"));
            }

            var result = await _stripeAccountService.ListBankAccounts(AccountId);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo { Message = result.Message });
            }

            return Ok((List<BankAccountAttachedViewModel>)result.Payload);
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> ConnectWebhook()
        {
            using (var sr = new StreamReader(HttpContext.Request.Body))
            {
                var json = await sr.ReadToEndAsync();
                var result = _stripeEventHandler.HandleConnectEvent(json, Request.Headers["Stripe-Signature"]);

                if (!result.Succeeded)
                {
                    return BadRequest(new ErrorInfo(result.Message));
                }

                return Ok();
            }
        }

        [AllowAnonymous]
        [HttpPost("connectedAccountWebhook")]
        public async Task<IActionResult> ConnectWebhookForConnectedAccount()
        {
            using (var sr = new StreamReader(HttpContext.Request.Body))
            {
                var json = await sr.ReadToEndAsync();
                var result = _stripeEventHandler.HandleStandardConnectEvent(json, Request.Headers["Stripe-Signature"]);
                if (!result.Succeeded)
                {
                    return BadRequest(new ErrorInfo(result.Message));
                }
                return Ok();
            }
        }
    }

}
