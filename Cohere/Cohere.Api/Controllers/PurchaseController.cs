using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Api.Utils.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service;
using Cohere.Entity.Enums.Contribution;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers
{
    [Authorize(Roles = "Client")]
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseController : CohereController
    {
        private readonly ContributionPurchaseService _contributionPurchaseService;
        private readonly IValidator<BookOneToOneTimeViewModel> _bookOneToOneTimeValidator;
        private readonly IValidator<PurchaseCourseContributionViewModel> _purchaseCourseContributionViewModelValidator;
        private readonly IValidator<PurchaseOneToOnePackageViewModel> _purchaseOneToOnePackageValidator;

        private readonly IValidator<PurchaseOneToOneMonthlySessionSubscriptionViewModel>
            _purchaseOneToOneMonthlySessionSubscriptionValidator;

        private readonly IValidator<PurchaseMembershipContributionViewModel>
            _purchaseMembershipContributionViewModelValidator;

        private readonly IValidator<PurchaseCommunityContributionViewModel>
            _purchaseCommunityContributionViewModelValidator;

        private readonly StripeEventHandler _stripeEventHandler;

        public PurchaseController(
            ContributionPurchaseService contributionPurchaseService,
            IValidator<BookOneToOneTimeViewModel> bookOneToOneTimeValidator,
            IValidator<PurchaseCourseContributionViewModel> purchaseCourseContributionViewModelValidator,
            IValidator<PurchaseOneToOnePackageViewModel> purchaseOneToOnePackageValidator,
            IValidator<PurchaseOneToOneMonthlySessionSubscriptionViewModel>
                purchaseOneToOneMonthlySessionSubscriptionValidator,
            IValidator<PurchaseMembershipContributionViewModel> purchaseMembershipContributionViewModelValidator,
            IValidator<PurchaseCommunityContributionViewModel> purchaseCommunityContributionViewModelValidator,
            StripeEventHandler stripeEventHandler)
        {
            _contributionPurchaseService = contributionPurchaseService;
            _bookOneToOneTimeValidator = bookOneToOneTimeValidator;
            _purchaseCourseContributionViewModelValidator = purchaseCourseContributionViewModelValidator;
            _purchaseOneToOnePackageValidator = purchaseOneToOnePackageValidator;
            _purchaseOneToOneMonthlySessionSubscriptionValidator = purchaseOneToOneMonthlySessionSubscriptionValidator;
            _purchaseMembershipContributionViewModelValidator = purchaseMembershipContributionViewModelValidator;
            _purchaseCommunityContributionViewModelValidator = purchaseCommunityContributionViewModelValidator;
            _stripeEventHandler = stripeEventHandler;
        }

        [HttpPost("membership")]
        public async Task<IActionResult> MembershipContributionPurchase(
            [FromBody] PurchaseMembershipContributionViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseMembershipContributionViewModelValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var paymentOption = Enum.Parse<PaymentOptions>(model.PaymentOption);

            var result = await _contributionPurchaseService.SubscribeToMembershipContributionAsync(
                AccountId,
                model.ContributionId,
                model.CouponId,
                paymentOption,
                model.AccessCode);

            return result.ToActionResult();
        }

        [HttpPost("community")]
        public async Task<IActionResult> CommunityContributionPurchase(
            [FromBody] PurchaseCommunityContributionViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseCommunityContributionViewModelValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var paymentOption = Enum.Parse<PaymentOptions>(model.PaymentOption);

            var result = await _contributionPurchaseService.SubscribeToCommunityContributionAsync(
                AccountId,
                model.ContributionId,
                model.CouponId,
                paymentOption,
                model.AccessCode);

            return result.ToActionResult();
        }

        [HttpPost("membership/academy/{contributionId}")]
        public async Task<IActionResult> EnrollInAcademyMembership([FromRoute] string contributionId)
        {
            var result = await _contributionPurchaseService.EnrollAcademyMembership(contributionId, AccountId);

            return result.ToActionResult();
        }

        [HttpPost("course")]
        public async Task<IActionResult> CourseContributionPurchase(
            [FromBody] PurchaseCourseContributionViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseCourseContributionViewModelValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.ToString()));
            }

            var paymentOptions = Enum.Parse<PaymentOptions>(model.PaymentOptions);
            OperationResult result;
            if (paymentOptions == PaymentOptions.EntireCourse)
            {
                result = await _contributionPurchaseService.PurchaseEntireCourseContributionAsync(AccountId, model.ContributionId, model.CouponId);
            }
            else if (paymentOptions == PaymentOptions.SplitPayments)
            {
                result = await _contributionPurchaseService.SubscribeToCourseContributionSplitPaymentsAsync(AccountId,
                    model.ContributionId, model.PaymentMethodId);
            }
            else
            {
                result = OperationResult.Failure("Unsupported PaymentOptions type.");
            }

            return result.ToActionResult();
        }

        [HttpPost("one-to-one")]
        public async Task<IActionResult> OneToOneContributionPurchase([FromBody] BookOneToOneTimeViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _bookOneToOneTimeValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.Errors.ToString()));
            }

            var result =
                await _contributionPurchaseService.PurchaseOneToOneContributionAsync(AccountId, model);

            return result.ToActionResult();
        }

        [HttpPost("one-to-one/monthly-session-subscription")]
        public async Task<IActionResult> OneToOneContributionMonthlySessionSubscription([FromBody] PurchaseOneToOneMonthlySessionSubscriptionViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseOneToOneMonthlySessionSubscriptionValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.Errors.ToString()));
            }

            var result = await _contributionPurchaseService.PurchaseOneToOneMonthlySessionSubscriptionAsync(AccountId, model.ContributionId, model.PaymentMethodId, model.CouponId);

            return result.ToActionResult();
        }

        [HttpPost("one-to-one/package")]
        public async Task<IActionResult> OneToOneContributionPackagePurchase([FromBody] PurchaseOneToOnePackageViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseOneToOnePackageValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.Errors.ToString()));
            }

            var result = await _contributionPurchaseService.OldPurchaseOneToOnePackageAsync(AccountId, model.ContributionId, model.CouponId);

            return result.ToActionResult();
        }

        [HttpPost("one-to-one/pkg")] //Duplicate as of [HttpPost("one-to-one/package")], because of frontend requirement
        public async Task<IActionResult> OneToOneContributionPurchasePackage([FromBody] PurchaseOneToOnePackageViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var validationResult = await _purchaseOneToOnePackageValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorInfo(validationResult.Errors.ToString()));
            }

            var result = await _contributionPurchaseService.PurchaseOneToOnePackageAsync(AccountId, model.ContributionId, model.CouponId, model.AccessCode);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("membership/{contributionId}/{paymentOptions}/{coupnoId?}")]
        public async Task<IActionResult> MembershipContributionPurchaseDetails(
            string contributionId,
            string paymentOptions, string coupnoId)
        {
            if (string.IsNullOrWhiteSpace(contributionId)
                || string.IsNullOrWhiteSpace(paymentOptions)
                || !Enum.TryParse<PaymentOptions>(paymentOptions, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result =
                await _contributionPurchaseService.GetMembershipContributionPurchaseDetailsAsync(contributionId,
                    paymentOptionsEnum, coupnoId);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("community/{contributionId}/{paymentOptions}/{coupnoId?}")]
        public async Task<IActionResult> CommunityContributionPurchaseDetails(
            string contributionId,
            string paymentOptions, string coupnoId)
        {
            if (string.IsNullOrWhiteSpace(contributionId)
                || string.IsNullOrWhiteSpace(paymentOptions)
                || !Enum.TryParse<PaymentOptions>(paymentOptions, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result =
                await _contributionPurchaseService.GetMembershipContributionPurchaseDetailsAsync(contributionId,
                    paymentOptionsEnum, coupnoId);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("course/{contributionId}/{paymentOptions}/{coupnoId?}")]
        public async Task<IActionResult> CourseContributionPurchaseDetails(string contributionId, string paymentOptions, string coupnoId)
        {
            if (string.IsNullOrWhiteSpace(contributionId)
                || string.IsNullOrWhiteSpace(paymentOptions)
                || !Enum.TryParse<PaymentOptions>(paymentOptions, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result =
                await _contributionPurchaseService.GetCourseContributionPurchaseDetailsAsync(contributionId,
                    paymentOptionsEnum, coupnoId);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("one-to-one/{contributionId}/{paymentOptions}/{coupnoId?}")]
        public async Task<IActionResult> OneToOneContributionPurchaseDetails(string contributionId,
            string paymentOptions, string coupnoId)
        {
            if (string.IsNullOrWhiteSpace(contributionId)
                || string.IsNullOrWhiteSpace(paymentOptions)
                || !Enum.TryParse<PaymentOptions>(paymentOptions, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result =
                await _contributionPurchaseService.GetOneToOneContributionPurchaseDetailsAsync(contributionId,
                    paymentOptionsEnum, coupnoId);

            return result.ToActionResult();
        }

        [HttpPost("proceed")]
        public async Task<IActionResult> GetPaymentSecret([FromBody] PurchaseContributionViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.ContributionId))
            {
                return BadRequest();
            }

            var result = await _contributionPurchaseService.GetClientSecretAsync(AccountId, model.ContributionId);

            return result.ToActionResult();
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> CancelPayment([FromBody] PurchaseContributionViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.ContributionId))
            {
                return BadRequest();
            }

            var result = await _contributionPurchaseService.CancelPurchasingAsync(AccountId, model.ContributionId);

            return result.ToActionResult();
        }

        [HttpPost("cancleOneToOnePackagePayment")]
        public async Task<IActionResult> CancelOneToOnePackagePayment([FromBody] CancelOneToOneReservation model)
        {
            if (model == null || string.IsNullOrEmpty(model.ContributionId) || model.Created == default)
            {
                return BadRequest();
            }

            var result =
                await _contributionPurchaseService.CancelOneToOnePackageReservation(AccountId, model.ContributionId,
                    model.Created);

            return result.ToActionResult();
        }

        [HttpPost("cancelOneToOneReservation")]
        public async Task<IActionResult> CancelOneToOneReservation([FromBody] CancelOneToOneReservation model)
        {
            if (model == null ||
                string.IsNullOrEmpty(model.BookedTimeId) ||
                string.IsNullOrEmpty(model.ContributionId) ||
                model.Created == default)
            {
                return BadRequest();
            }

            var result = await _contributionPurchaseService.CancelOneToOneReservation(AccountId, model.ContributionId,
                model.BookedTimeId, model.Created);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> ContributionPurchaseWebhook()
        {
            if (string.IsNullOrWhiteSpace(Request.Headers["Stripe-Signature"]))
            {
                return BadRequest();
            }

            using var sr = new StreamReader(HttpContext.Request.Body);
            var json = await sr.ReadToEndAsync();
            var result = _stripeEventHandler.HandleAccountEvent(json, Request.Headers["Stripe-Signature"]);

            return result.ToActionResult();
        }

        [AllowAnonymous]
        [HttpPost("connectedAccountWebhook")]
        public async Task<IActionResult> ContributionPurchaseWebhookForConnectedAccount()
        {
            if (string.IsNullOrWhiteSpace(Request.Headers["Stripe-Signature"]))
            {
                return BadRequest();
            }
            using var sr = new StreamReader(HttpContext.Request.Body);
            var json = await sr.ReadToEndAsync();
            var result = _stripeEventHandler.HandleStandardAccountEvent(json, Request.Headers["Stripe-Signature"]);
            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpGet("ListMyCoaches")]
        public async Task<IActionResult> GetCoaches()
        {
            var result = await _contributionPurchaseService.ListMyCoaches(AccountId);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("cancel/membership/{contributionId}")]
        public async Task<IActionResult> CancelMembership([FromRoute] string contributionId)
        {
            var result = await _contributionPurchaseService.CancelMembership(AccountId, contributionId);
            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("upgrade/membership")]
        public async Task<IActionResult> UpgradeMembershipPlan([FromBody] UpgradeMembershipModel model)
        {
            if (!Enum.TryParse<PaymentOptions>(model.PaymentOption, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result = await _contributionPurchaseService.UpgradeMembershipPlan(
                AccountId,
                model.ContributionId,
                paymentOptionsEnum);

            return result.ToActionResult();
        }

        [Authorize(Roles = "Client")]
        [HttpPost("course/checkout")]
        public async Task<IActionResult> EntireCourseCheckout(CheckoutViewModel checkoutViewModel)
        {
            if (!Enum.TryParse<PaymentOptions>(checkoutViewModel.PaymentOption, out var paymentOptionsEnum))
            {
                return BadRequest();
            }

            var result = await _contributionPurchaseService.PurchaseLiveCourseWithCheckout(
                checkoutViewModel.ContributionId,
                purchaseId: null,
                AccountId,
                paymentOptionsEnum,
                checkoutViewModel.CouponId,
                checkoutViewModel.AccessCode);
            
            return result.ToActionResult();
        }


        [Authorize(Roles = "Client")]
        [HttpPost("course/invoice")]
        public async Task<IActionResult> EntireCourseInvoice(CheckoutViewModel checkoutViewModel)
        {
            if (!Enum.TryParse<PaymentOptions>(checkoutViewModel.PaymentOption, out var paymentOptionsEnum))
            {
                return BadRequest(); 
            }

            var result = await _contributionPurchaseService.PurchaseLiveCourseWithInvoice(
                checkoutViewModel.ContributionId,
                purchaseId: null,
                AccountId,
                paymentOptionsEnum,
                checkoutViewModel.CouponId);
            return result.ToActionResult();
        }

        [HttpGet("TenRecentSales")]
        public async Task<IActionResult> TenRecentSales(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new ErrorInfo { Message = "User Id is null"});
            }
            var result = await _contributionPurchaseService.TenRecentSales(userId);
            return result.ToActionResult();
        }
    }
   
    public class OneToOneCheckoutViewModel
    {
        public string ContributionId { get; set; }
        
        public string BookedTimeId { get; set; }
        
        public string PaymentOption { get; set; }
        
        public string CouponId { get; set; }
    }
    
    public class CheckoutViewModel
    {
        public string ContributionId { get; set; }

        public string PaymentOption { get; set; }
        
        public string CouponId { get; set; }

        public string AccessCode { get; set; }
    }

    public class InvoiceCreationViewModel : CheckoutViewModel
    {
        public string CountryId { get; set; }
        public string PostalCode { get; set; } = null;
    }
}
