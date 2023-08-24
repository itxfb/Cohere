using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Castle.Core.Resource;
using Cohere.Domain.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.BackgroundExecution;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Service.Users;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Ical.Net.CalendarComponents;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stripe;
using Twilio.Exceptions;
using Twilio.Rest.Verify.V2.Service;
using Account = Cohere.Entity.Entities.Account;
using Coupon = Cohere.Entity.Entities.Coupon;
using StripeEvent = Stripe.Event;

namespace Cohere.Domain.Service
{
    public class ContributionPurchaseService
    {
        private readonly SetupIntentService _setupIntentService;
        private readonly CustomerService _customerService;
        private readonly IStripeService _stripeService;
        private readonly StripeAccountService _stripeAccountService;
        private readonly IPaymentSystemFeeService _calculationFeeService;
        private readonly IPricingCalculationService _pricingCalculationService;
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;
        private readonly IPayoutService _payoutService;
        private readonly IJobScheduler _jobScheduler;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IChatService _chatService;
        private readonly ILogger<ContributionPurchaseService> _logger;
        private readonly IContributionRootService _contributionRootService;
        private readonly ICohealerIncomeService _cohealerIncomeService;
        private readonly ISynchronizePurchaseUpdateService _synchronizePurchaseUpdateService;
        private readonly IAffiliateCommissionService _affiliateCommissionService;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;
        private readonly BalanceTransactionService _balanceTransactionService;
        private readonly ICouponService _couponService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICommonService _commonService;
        private readonly int _paymentSessionLifetimeSeconds;
        private readonly IZoomService _zoomService;
        private readonly IContributionBookingService _contributionBookingService;
        private readonly IFCMService _fcmService;
        private readonly IProfilePageService _profilePageService;

        public const string PaymentSessionLifetimeSeconds = nameof(PaymentSessionLifetimeSeconds);

        public ContributionPurchaseService(
            SetupIntentService setupIntentService,
            CustomerService customerService,
            IStripeService stripeService,
            IPaymentSystemFeeService calculationFeeService,
            IPricingCalculationService pricingCalculationService,
            InvoiceService invoiceService,
            ProductService productService,
            IPayoutService payoutService,
            IJobScheduler jobScheduler,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IChatService chatService,
            ILogger<ContributionPurchaseService> logger,
            Func<string, int> integersResolver,
            IContributionRootService contributionRootService,
            ICohealerIncomeService cohealerIncomeService,
            ISynchronizePurchaseUpdateService synchronizePurchaseUpdateService,
            IAffiliateCommissionService affiliateCommissionService,
            IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService,
            BalanceTransactionService balanceTransactionService,
            ICouponService couponService,
            IActiveCampaignService activeCampaignService,
            ICommonService commonService,
            IZoomService zoomService, StripeAccountService stripeAccountService,
            IBookIfSingleSessionTimeJob bookIfSingleSessionTimeJob,
            IContributionBookingService contributionBookingService,
            IFCMService fcmService,
            IProfilePageService profilePageService)
        {
            _setupIntentService = setupIntentService;
            _customerService = customerService;
            _stripeService = stripeService;
            _calculationFeeService = calculationFeeService;
            _pricingCalculationService = pricingCalculationService;
            _invoiceService = invoiceService;
            _productService = productService;
            _payoutService = payoutService;
            _jobScheduler = jobScheduler;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _chatService = chatService;
            _logger = logger;
            _contributionRootService = contributionRootService;
            _cohealerIncomeService = cohealerIncomeService;
            _synchronizePurchaseUpdateService = synchronizePurchaseUpdateService;
            _affiliateCommissionService = affiliateCommissionService;
            _paidTiersService = paidTiersService;
            _balanceTransactionService = balanceTransactionService;
            _couponService = couponService;
            _activeCampaignService = activeCampaignService;
            _paymentSessionLifetimeSeconds = integersResolver.Invoke(PaymentSessionLifetimeSeconds);
            _commonService = commonService;
            _zoomService = zoomService;
            _stripeAccountService = stripeAccountService;
            _contributionBookingService = contributionBookingService;
            _profilePageService = profilePageService;
            _fcmService = fcmService;
        }

        public async Task<OperationResult> SubscribeToCommunityContributionAsync(string accountId, string contributionId, string couponId, PaymentOptions paymentOption, string accessCode)
        {
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);
            var contribution = await _contributionRootService.GetOne(contributionId);
            

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(clientUser, clientAccount, contribution);

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == coachUser.AccountId);
            if (contribution == null || contribution.Status != ContributionStatuses.Approved)
            {
                return OperationResult<string>.Failure("Contribution which Id was provided was not found");
            }

            if (contribution.Type != nameof(ContributionCommunity))
            {
                return OperationResult<string>.Failure("Only community contribution supported here");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOption) && paymentOption != PaymentOptions.Free)
            {
                return OperationResult<string>.Failure(
                    $"'{PaymentOptions.SplitPayments.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
            }

            if (contribution.PaymentType == PaymentTypes.Advance && (!coachUser.IsStandardAccount || string.IsNullOrEmpty(coachUser.StripeStandardAccountId)))
            {
                return OperationResult<string>.Failure("unsupported payment type for contribtuion", "unsupported payment type for contribtuion. Advance payment is enable for the Stripe standard account only");
            }

            var customerStripeAccountId = clientUser.CustomerStripeAccountId;

            if (customerStripeAccountId is null)
            {
                _logger.LogError($"customer with accountId {clientUser.AccountId} has no Stripe Account");
                return OperationResult<string>.Failure("Stripe customer is not attached to the user");
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(x => x.ContributionId == contributionId && x.ClientId == clientUser.Id);

            var purchase = purchases.OrderByDescending(e => e.CreateTime).FirstOrDefault();

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (purchaseVm != null)
            {
                if (purchaseVm?.Subscription?.Status == "active")
                {
                    return OperationResult<string>.Failure("You've already purchased this contribution");
                }
            }

            // check if 100% coupon code applies here
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased community contribution through 100% off coupon", purchaseResult.Payload);
                    }
                }
            }

            // check if session payment option is free
            if (paymentOption == PaymentOptions.Free)
            {
                var isAccessCodeValid = validateAccessCode(accessCode, contributionId);
                if (isAccessCodeValid)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased Free community contribution with Access Code", purchaseResult.Payload);
                    }
                }
            }

            var billingInfo = contribution.PaymentInfo.MembershipInfo.ProductBillingPlans[paymentOption];

            var model = new CreateCheckoutSessionModel
            {
                ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                ServiceAgreementType = coachUser.ServiceAgreementType,
                StripeCustomerId = customerStripeAccountId,
                PriceId = billingInfo.ProductBillingPlanId,
                BillingInfo = billingInfo,
                ContributionId = contribution.Id,
                PaymentOption = paymentOption,
                CouponId = couponId,
                IsStandardAccount = coachUser.IsStandardAccount,
                StripeStandardAccountId = coachUser.StripeStandardAccountId,
                paymentType = contribution.PaymentType,
                ClientEmail = clientAccount.Email,
                ClientFirstName = clientUser.FirstName,
                ClientLastName = clientUser.LastName,
                CoachEmail = coachAccount.Email,
                ContributionTitle = contribution.Title,
                TaxType = contribution.TaxType
            };

            var createCheckoutSessionResult = await _stripeService.CreateSubscriptionCheckoutSession(model);

            if (createCheckoutSessionResult.Succeeded)
            {
                if (contribution.PaymentType == PaymentTypes.Advance)
                {
                    return OperationResult<string>.Success(String.Empty, (string)createCheckoutSessionResult.Payload.RawJObject["url"]);
                }
                return OperationResult<string>.Success(String.Empty, createCheckoutSessionResult.Payload.Id);
            }
            else
            {
                return OperationResult<string>.Failure(createCheckoutSessionResult.Message);
            }
        }

        public async Task<OperationResult> GetCourseContributionPurchaseDetailsAsync(
            string contributionId,
            PaymentOptions paymentOptions,
            string couponId)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution == null)
            {
                return OperationResult.Failure("Contribution which Id was provided was not found");
            }

            decimal couponDiscountInPercentage = 1m;
            if (!string.IsNullOrEmpty(couponId))
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOptions);
                if (validateCouponResult?.PercentAmount > 0)
                {
                    if (validateCouponResult.PercentAmount == 100)
                    {
                        return OperationResult.Success(null, new ContributionPaymentDetailsViewModel()
                        {
                            Option = paymentOptions.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = 0,
                            Price = 0,
                            DueNow = 0,
                            DueNowWithCouponDiscountAmount = 0,
                            DueLater = 0
                        });
                    }
                    couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                }
            }

            ContributionPaymentDetailsViewModel result = null;

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOptions))
            {
                return OperationResult.Failure(
                    $"'{PaymentOptions.EntireCourse.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
            }

            if (paymentOptions == PaymentOptions.SplitPayments)
            {
                if (contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId == null)
                {
                    return OperationResult.Failure(
                        $"'{nameof(contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId)}' must be not null");
                }

                if (!contribution.PaymentInfo.SplitNumbers.HasValue)
                {
                    return OperationResult.Failure("Contribution split numbers are not specified");
                }

                var billingPlanInfo = contribution.PaymentInfo.BillingPlanInfo;
                result = new ContributionPaymentDetailsViewModel
                {
                    Option = contribution.PaymentInfo.SplitPeriod.ToString(),
                    Currency = contribution.DefaultCurrency,
                    PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : (billingPlanInfo.BillingPlanGrossCost * couponDiscountInPercentage) - (billingPlanInfo.BillingPlanPureCost * couponDiscountInPercentage),
                    Price = billingPlanInfo.TotalBillingPureCost * couponDiscountInPercentage,
                    DueNow = billingPlanInfo.BillingPlanGrossCost,
                    DueNowWithCouponDiscountAmount = billingPlanInfo.BillingPlanGrossCost * couponDiscountInPercentage,
                    DueLater = (billingPlanInfo.TotalBillingGrossCost * couponDiscountInPercentage) - (billingPlanInfo.BillingPlanGrossCost * couponDiscountInPercentage)
                };
            }
            else if (paymentOptions == PaymentOptions.EntireCourse)
            {
                var discountPercentage = contribution.PaymentInfo.PackageSessionDiscountPercentage;
                var discount = (100m - discountPercentage) / 100 ?? 1m;
                var priceWithDiscount = contribution.PaymentInfo.Cost.Value * discount;
                priceWithDiscount = priceWithDiscount * couponDiscountInPercentage;
                var priceWithDiscountWithoutCouponDiscount = contribution.PaymentInfo.Cost.Value;
                priceWithDiscountWithoutCouponDiscount = priceWithDiscountWithoutCouponDiscount * discount;
                var purchaseAmount = priceWithDiscount * _stripeService.SmallestCurrencyUnit;
                var purchaseAmountWithoutCouponDiscount = priceWithDiscountWithoutCouponDiscount * _stripeService.SmallestCurrencyUnit;
                var price = _calculationFeeService.CalculateGrossAmountAsLong(
                                purchaseAmount,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                var priceWithoutDiscount = _calculationFeeService.CalculateGrossAmountAsLong(
                                purchaseAmountWithoutCouponDiscount,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                var Gross_Price = priceWithoutDiscount * couponDiscountInPercentage;

                result = new ContributionPaymentDetailsViewModel
                {
                    Option = PaymentOptions.EntireCourse.ToString(),
                    Currency = contribution.DefaultCurrency,
                    PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : Gross_Price - priceWithDiscount,
                    Price = purchaseAmount / _stripeService.SmallestCurrencyUnit,
                    //DueNow = decimal.Round((purchaseAmountWithoutCouponDiscount / _stripeService.SmallestCurrencyUnit) +
                    //(afterCouponAppliedFee / couponDiscountInPercentage), 2),
                    DueNow= priceWithoutDiscount,
                    DueNowWithCouponDiscountAmount = Gross_Price,
                    DueLater = 0
                };
            }
            else
            {
                return OperationResult.Failure("Unsupported PaymentOptions type");
            }

            return OperationResult.Success(null, result);
        }

        public async Task<OperationResult<List<ContributionPurchaseModel>>> TenRecentSales(string userId)
        {
            List<ContributionPurchaseModel> mappedContributions = new List<ContributionPurchaseModel>();
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributorId == userId);
            var purchaseVm = _mapper.Map<List<PurchaseViewModel>>(purchases).OrderByDescending(m => m.CreateTime).ToList();
            var filteredPurchase = purchaseVm.Where(m => m.Payments?.LastOrDefault()?.PurchaseAmount > 0).Take(10);

            if (filteredPurchase.Count() > 0)
            {
                foreach (var p in filteredPurchase)
                {
                    ContributionPurchaseModel model = new ContributionPurchaseModel();

                    var clientData = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.Id == p.ClientId);
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(m => m.Id == p.ContributionId);

                    string chatId = string.Empty;

                    model.EarnedRevenue = await _cohealerIncomeService.GetSingleClientRevenueAsync(contribution.Id, p.ClientId);
                    model.FirstName = clientData?.FirstName;
                    model.LastName = clientData?.LastName;
                    model.AvatarUrl = clientData?.AvatarUrl;
                    model.CreateTime = p.CreateTime;
                    model.ClientId = clientData?.Id;
                    model.Title = contribution.Title;
                    model.ContributionId = contribution.Id;
                    model.DefaultSymbol = contribution.DefaultSymbol;

                    if (Convert.ToBoolean(!contribution.Chat?.CohealerPeerChatSids?.ContainsKey(clientData.Id)))
                    {
                        _chatService.AddClientToContributionRelatedChat(clientData.Id, contribution).GetAwaiter().GetResult();
                    }
                    if (contribution.Chat != null && contribution.Chat.CohealerPeerChatSids != null)
                    {
                        model.Sid = contribution.Chat.CohealerPeerChatSids.TryGetValue(clientData.Id, out chatId) ? chatId : string.Empty;
                    }
                    model.FriendlyName = contribution.Chat?.FriendlyName;
                    model.CohealerPeerChatSids = contribution.Chat?.CohealerPeerChatSids;
                    mappedContributions.Add(model);
                }
            }
            return OperationResult<List<ContributionPurchaseModel>>.Success(mappedContributions.OrderByDescending(m => m.CreateTime).ToList());
        }

        public async Task<OperationResult<ContributionPaymentDetailsViewModel>>
            GetMembershipContributionPurchaseDetailsAsync(string contributionId, PaymentOptions paymentOption, string couponId)
        {
            var allowedPaymentOptions = new[]
            {
                PaymentOptions.DailyMembership,
                PaymentOptions.WeeklyMembership,
                PaymentOptions.MonthlyMembership,
                PaymentOptions.YearlyMembership,
                PaymentOptions.MembershipPackage
            };

            if (!allowedPaymentOptions.Contains(paymentOption))
            {
                return OperationResult<ContributionPaymentDetailsViewModel>.Failure("not allowed payment option");
            }

            var contribution = await _contributionRootService.GetOne(e => e.Id == contributionId);

            if (contribution is null)
            {
                return OperationResult<ContributionPaymentDetailsViewModel>.Failure("contribution not found");
            }

            decimal couponDiscountInPercentage = 1m;
            if (!string.IsNullOrEmpty(couponId))
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                if (validateCouponResult?.PercentAmount > 0)
                {
                    if (validateCouponResult.PercentAmount == 100)
                    {
                        return OperationResult<ContributionPaymentDetailsViewModel>.Success(null, new ContributionPaymentDetailsViewModel()
                        {
                            Option = paymentOption.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = 0,
                            Price = 0,
                            DueNow = 0,
                            DueNowWithCouponDiscountAmount = 0,
                            DueLater = 0
                        });
                    }
                    couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                }
            }

            var billingPlan = contribution.PaymentInfo.MembershipInfo.ProductBillingPlans[paymentOption];

            var discountPercentage = contribution.PaymentInfo.PackageSessionDiscountPercentage;
            var discount = (100m - discountPercentage) / 100 ?? 1m;
            var priceWithDiscount = billingPlan.BillingPlanPureCost * couponDiscountInPercentage;
            var priceWithDiscountWithoutCouponDiscount = billingPlan.BillingPlanPureCost;
            priceWithDiscount = priceWithDiscount * discount;
            priceWithDiscountWithoutCouponDiscount = priceWithDiscountWithoutCouponDiscount * discount;
            var purchaseAmount = priceWithDiscount * _stripeService.SmallestCurrencyUnit;
            var purchaseAmountWithoutCouponDiscount = priceWithDiscountWithoutCouponDiscount * _stripeService.SmallestCurrencyUnit;
            var price = _calculationFeeService.CalculateGrossAmountAsLong(
                            purchaseAmountWithoutCouponDiscount,
                            contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                        / _stripeService.SmallestCurrencyUnit;
            price = decimal.Round(price * couponDiscountInPercentage, 2);
            var priceWithoutDiscount = _calculationFeeService.CalculateGrossAmountAsLong(
                            purchaseAmountWithoutCouponDiscount,
                            contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                        / _stripeService.SmallestCurrencyUnit;
            var afterCouponAppliedFee = _calculationFeeService.CalculateFee(purchaseAmount,
                            contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId) / _stripeService.SmallestCurrencyUnit;
            var result = new ContributionPaymentDetailsViewModel()
            {
                Currency = contribution.DefaultCurrency,
                PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : price - priceWithDiscount,
                Price = purchaseAmount / _stripeService.SmallestCurrencyUnit,
                //DueNow = decimal.Round((purchaseAmountWithoutCouponDiscount / _stripeService.SmallestCurrencyUnit) +
                //        (afterCouponAppliedFee / couponDiscountInPercentage), 2),
                DueNow = priceWithoutDiscount,
                DueNowWithCouponDiscountAmount = price,



                //DueNow = billingPlan.BillingPlanGrossCost,
                //DueNowWithCouponDiscountAmount = billingPlan.BillingPlanGrossCost * couponDiscountInPercentage,
                //Option = paymentOption.ToString(),
                //Currency = _stripeService.Currency,
                //// TODO: change calculation with discount to be correct (see GetCourseContributionPurchaseDetailsAsync)
                //PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : 
                //    (billingPlan.BillingPlanGrossCost * couponDiscountInPercentage) - (billingPlan.BillingPlanPureCost * couponDiscountInPercentage),
                //PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : price - priceWithDiscount,
            };

            return OperationResult<ContributionPaymentDetailsViewModel>.Success(result);
        }

        public async Task<OperationResult> GetOneToOneContributionPurchaseDetailsAsync(
            string contributionId,
            PaymentOptions paymentOptions,
            string couponId)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution == null)
            {
                return OperationResult.Failure("Contribution which Id was provided was not found");
            }

            ContributionPaymentDetailsViewModel result = null;

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOptions))
            {
                return OperationResult.Failure(
                    $"'{PaymentOptions.EntireCourse.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
            }

            var paymentInfo = contribution.PaymentInfo;

            decimal couponDiscountInPercentage = 1m;
            if (!string.IsNullOrEmpty(couponId))
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOptions);
                if (validateCouponResult?.PercentAmount > 0)
                {
                    if (validateCouponResult.PercentAmount == 100)
                    {
                        return OperationResult<ContributionPaymentDetailsViewModel>.Success(null, new ContributionPaymentDetailsViewModel()
                        {
                            Option = paymentOptions.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = 0,
                            Price = 0,
                            DueNow = 0,
                            DueNowWithCouponDiscountAmount = 0,
                            DueLater = 0
                        });
                    }
                    couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                }
            }

            try
            {
                switch (paymentOptions)
                {
                    case PaymentOptions.PerSession:
                        var pureCost = paymentInfo.Cost.Value * couponDiscountInPercentage;
                        var pureCostWithoutCouonDiscount = paymentInfo.Cost.Value;

                        var grossCost =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                pureCost * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                        var grossCostWithoutCouonDiscount =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                pureCostWithoutCouonDiscount * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                        var grossCost_WithCoupon = grossCostWithoutCouonDiscount * couponDiscountInPercentage;

                        result = new ContributionPaymentDetailsViewModel
                        {
                            Option = paymentOptions.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : grossCost_WithCoupon - pureCost,
                            Price = pureCost,
                            DueNow = grossCostWithoutCouonDiscount,
                            DueNowWithCouponDiscountAmount = grossCost_WithCoupon,
                            DueLater = 0
                        };
                        break;
                    case PaymentOptions.SessionsPackage:
                        if (!paymentInfo.PackageSessionNumbers.HasValue) 
                        {
                            return OperationResult.Failure(
                                $"Unable to calculate package cost. '{nameof(paymentInfo.PackageSessionNumbers)}' is not specified");
                        }
                        var actualCostOfPackage = paymentInfo.PackageCost.HasValue ? paymentInfo.PackageCost.Value : paymentInfo.Cost.Value * paymentInfo.PackageSessionNumbers.Value;

                        var packagePureCost = actualCostOfPackage;
                        packagePureCost *= couponDiscountInPercentage;
                        var packagePureCostWithoutCouponDiscount = actualCostOfPackage;

                        if (paymentInfo.PackageSessionDiscountPercentage.HasValue)
                        {
                            packagePureCost -= (packagePureCost * paymentInfo.PackageSessionDiscountPercentage.Value / 100);
                            packagePureCostWithoutCouponDiscount -= (packagePureCostWithoutCouponDiscount * paymentInfo.PackageSessionDiscountPercentage.Value / 100);
                        }

                        var packageGrossCost =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                packagePureCost * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                        var packageGrossCostWithoutCouponDiscount =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                packagePureCostWithoutCouponDiscount * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;

                        result = new ContributionPaymentDetailsViewModel
                        {
                            Option = paymentOptions.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : packageGrossCost - packagePureCost,
                            Price = packagePureCost,
                            DueNow = packageGrossCostWithoutCouponDiscount,
                            DueNowWithCouponDiscountAmount = packageGrossCost,
                            DueLater = 0
                        };
                        break;
                    case PaymentOptions.MonthlySessionSubscription:

                        var validationResult = ValidateMonthlySessionSubscription(paymentInfo);

                        if (validationResult.Failed)
                        {
                            return validationResult;
                        }
                        var billingPlanInfo = contribution.PaymentInfo.BillingPlanInfo;

                        var Monthly_duration = (decimal)contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration;

                        //total
                        var Total_PureCost_monthly = billingPlanInfo.TotalBillingPureCost;
                        Total_PureCost_monthly *= couponDiscountInPercentage;
                        var Total_PureCostWithoutCouponDiscount_monthly = billingPlanInfo.TotalBillingPureCost;

                        //for each month
                        var PureCost_monthly = billingPlanInfo.BillingPlanPureCost;
                        PureCost_monthly *= couponDiscountInPercentage;
                        var PureCostWithoutCouponDiscount_monthly = billingPlanInfo.BillingPlanPureCost;

                        //each month gross_cost
                        var GrossCost_monthly =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                PureCost_monthly * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;
                        var GrossCostWithoutCouponDiscount_monthly =
                            _calculationFeeService.CalculateGrossAmountAsLong(
                                PureCostWithoutCouponDiscount_monthly * _stripeService.SmallestCurrencyUnit,
                                contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId)
                            / _stripeService.SmallestCurrencyUnit;

                        var Total_GrossCost_WithCoupon = billingPlanInfo.TotalBillingGrossCost * couponDiscountInPercentage;
                        var GrossCost_WithCoupon = GrossCostWithoutCouponDiscount_monthly * couponDiscountInPercentage;

                        result = new ContributionPaymentDetailsViewModel
                        {
                            Option = PaymentSplitPeriods.Monthly.ToString(),
                            Currency = contribution.DefaultCurrency,
                            PlatformFee = contribution.PaymentInfo.CoachPaysStripeFee ? 0 : Total_GrossCost_WithCoupon - Total_PureCost_monthly,
                            Price = Total_PureCost_monthly,
                            DueNow = GrossCostWithoutCouponDiscount_monthly,
                            DueNowWithCouponDiscountAmount = GrossCost_WithCoupon,
                            DueLater = Total_GrossCost_WithCoupon- GrossCost_WithCoupon
                        };
                        break;
                    default:
                        return OperationResult.Failure("Unsupported PaymentOptions type");
                }
                return OperationResult.Success(null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, @$"ContributionPurchaseService.GetOneToOneContributionPurchaseDetailsAsync method exception occured: {ex.Message} {Environment.NewLine} 
                For Contribution_ID: {contribution.Id}");
                throw ex;
            }
          
            static OperationResult ValidateMonthlySessionSubscription(PaymentInfo paymentInfo)
            {
                if (paymentInfo.MonthlySessionSubscriptionInfo == null)
                {
                    return OperationResult.Failure(
                        $"Unable to calculate monthly subscription cost. '{nameof(paymentInfo.MonthlySessionSubscriptionInfo)}' is not specified");
                }

                if (!paymentInfo.MonthlySessionSubscriptionInfo.SessionCount.HasValue)
                {
                    return OperationResult.Failure(
                        $"Unable to calculate monthly subscription cost. '{nameof(paymentInfo.MonthlySessionSubscriptionInfo.SessionCount)}' is not specified");
                }

                if (!paymentInfo.MonthlySessionSubscriptionInfo.Duration.HasValue)
                {
                    return OperationResult.Failure(
                        $"Unable to calculate monthly subscription cost. '{nameof(paymentInfo.MonthlySessionSubscriptionInfo.Duration)}' is not specified");
                }

                if (!paymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice.HasValue)
                {
                    return OperationResult.Failure(
                        $"Unable to calculate monthly subscription cost. `{nameof(paymentInfo.MonthlySessionSubscriptionInfo.Duration)}` is not specified");
                }

                return OperationResult.Success();
            }
        }

        public async Task<OperationResult> GetClientSecretAsync(string accountId, string contributionId)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);

            if (contribution == null)
            {
                return OperationResult.Failure("Contribution which Id was provided was not found");
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            if (customerStripeAccountId == null)
            {
                _logger.LogError($"customer with accountId {user.AccountId} has no customer Stripe Account");
                return OperationResult.Failure("Stripe customer is not attached to the user");
            }

            if (purchase == null)
            {
                return OperationResult.Failure("Purchase was not found");
            }

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (purchaseVm.HasProcessingPayment)
            {
                return OperationResult.Failure("Contribution purchasing is in processing. Try later");
            }

            var requiresActionQuery = purchaseVm.Payments
                .Where(x =>
                    x.PaymentStatus != PaymentStatus.Canceled
                    && x.PaymentStatus != PaymentStatus.Succeeded
                    && x.PaymentStatus != PaymentStatus.RequiresPaymentMethod);

            //Proceeding of sessions package purchasing is only allowed for OneToOne contribution type
            //because the Client is able to have only one purchased package for one one-to-one contribution
            if (purchase.ContributionType == nameof(ContributionOneToOne))
            {
                requiresActionQuery = requiresActionQuery.Where(x => x.PaymentOption == PaymentOptions.SessionsPackage);
            }

            var payment = requiresActionQuery.OrderByDescending(x => x.DateTimeCharged).FirstOrDefault();

            if (payment == null)
            {
                return OperationResult.Failure("Payment was not found");
            }

            var paymentIntent = await _stripeService.GetPaymentIntentAsync(payment.TransactionId);
            var result = new ProceedPaymentViewModel
            {
                ClientSecret = paymentIntent.ClientSecret,
                IsCancellationAllowed = purchaseVm.Payments.All(x => x.PaymentStatus != PaymentStatus.Succeeded)
            };

            return OperationResult.Success(null, result);
        }

        public async Task<OperationResult<List<UserPreviewViewModel>>> ListMyCoaches(string accountId)
        {
            List<UserPreviewViewModel> coachesList = new List<UserPreviewViewModel>();
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(e =>
                e.ClientId == user.Id || e.ClientId.ToLower() == $"delete-{user.Id}");

            var purchaseVms = _mapper.Map<List<PurchaseViewModel>>(purchases);


            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));

            var purchasesWithAccess = purchaseVms.Where(e => e.HasAccessToContribution);
            var coachesUserIds = purchasesWithAccess.Select(e => e.ContributorId).Distinct();

            var contributionsIds = purchasesWithAccess.Select(m => m.ContributionId);
            var contributions = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(m => contributionsIds.Contains(m.Id));
            var contributionVm = _mapper.Map<List<ContributionBaseViewModel>>(contributions);
            var coachesUsers = await _unitOfWork.GetRepositoryAsync<User>().Get(e => coachesUserIds.Contains(e.Id));
            if (coachesUsers.Count() > 0)
            {
                foreach (var m in coachesUsers)
                {
                    UserPreviewViewModel coachModel = new UserPreviewViewModel();
                    coachModel.Id = m.Id;
                    coachModel.FirstName = m.FirstName;
                    coachModel.LastName = m.LastName;
                    coachModel.MiddleName = m.MiddleName;
                    coachModel.AvatarUrl = m.AvatarUrl;
                    coachModel.Sid = contributionVm?.FirstOrDefault(k => k.UserId == m.Id)?.Chat?.Sid;
                    coachModel.FriendlyName = contributionVm?.FirstOrDefault(k => k.UserId == m.Id)?.Chat?.FriendlyName;
                    coachModel.CohealerPeerChatSids = contributionVm?.FirstOrDefault(k => k.UserId == m.Id)?.Chat?.CohealerPeerChatSids;
                    coachesList.Add(coachModel);
                }
            }
            return OperationResult<List<UserPreviewViewModel>>.Success(String.Empty, coachesList);
        }

        public async Task<OperationResult> CancelPurchasingAsync(string accountId, string contributionId)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

            if (contribution == null)
            {
                return OperationResult.Failure("Contribution which Id was provided was not found");
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            if (customerStripeAccountId == null)
            {
                _logger.LogError($"customer with accountId {user.AccountId} has no customer Stripe Account");
                return OperationResult.Failure("Stripe customer is not attached to the user");
            }

            if (purchaseVm == null)
            {
                return OperationResult.Failure("Contribution related purchase was not found");
            }

            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (purchaseVm.HasProcessingPayment)
            {
                return OperationResult.Failure(
                    "Unable to cancel purchasing. Contribution payment is in processing. Please try later");
            }

            if (purchaseVm.HasSucceededPayment)
            {
                return OperationResult.Failure(
                    "Unable to cancel purchasing. You have already bought this contribution");
            }

            if (purchaseVm.RecentPaymentOption == PaymentOptions.SplitPayments && purchaseVm.SubscriptionId != null)
            {
                var subscriptionResult =
                    await _stripeService.GetProductPlanSubscriptionAsync(purchaseVm.SubscriptionId);

                if (!subscriptionResult.Succeeded)
                {
                    return subscriptionResult;
                }

                var subscription = subscriptionResult.Payload;

                if (subscription.Status != "canceled")
                {
                    var cancellationResult =
                        await _stripeService.CancelProductPlanSubscriptionScheduleAsync(subscription.Schedule.Id);

                    if (!cancellationResult.Succeeded)
                    {
                        return cancellationResult;
                    }
                }

                var latestInvoice = subscription.LatestInvoice;

                if (latestInvoice != null && latestInvoice.Status != "draft" && latestInvoice.Status != "void")
                {
                    var voidResult = await _stripeService.VoidInvoiceAsync(subscription.LatestInvoiceId);

                    if (!voidResult.Succeeded)
                    {
                        return voidResult;
                    }
                }

                purchaseVm.SubscriptionId = null;
            }

            if (purchaseVm.RecentPaymentOption == PaymentOptions.EntireCourse)
            {
                var payment = purchaseVm.Payments.FirstOrDefault(x => x.PaymentStatus != PaymentStatus.Canceled);

                if (payment == null)
                {
                    return OperationResult.Failure("Contribution related payment was not found");
                }

                var cancellationResult = await _stripeService.CancelPaymentIntentAsync(payment.TransactionId);

                if (!cancellationResult.Succeeded)
                {
                    return cancellationResult;
                }
            }

            purchase = _mapper.Map<Purchase>(purchaseVm);
            _synchronizePurchaseUpdateService.Sync(purchase);

            return OperationResult.Success(null);
        }

        public async Task<OperationResult> CancelOneToOnePackageReservation(
            string accountId,
            string contributionId,
            DateTime createdDate)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
            PurchasePayment payment = null;
            if (purchaseVm != null)
            {
                if (purchaseVm.HasProcessingPayment)
                {
                    return OperationResult.Failure("Contribution payment is in processing. Try later");
                }

                if (purchaseVm.HasUnconfirmedPayment)
                {
                    return OperationResult.Failure(
                        "Unable to purchase sessions package until you have unconfirmed payments");
                }

                //Attempt to find incomplete Payment and then to REUSE related Stripe Payment Intent
                payment = purchaseVm.Payments.FirstOrDefault(x =>
                    x.PaymentStatus == PaymentStatus.RequiresPaymentMethod &&
                    x.PaymentOption == PaymentOptions.SessionsPackage && x.DateTimeCharged == createdDate);
            }

            if (payment == null)
            {
                return OperationResult.Failure("can't find package payment intent");
            }

            var cancellationResult = await _stripeService.CancelPaymentIntentAsync(payment.TransactionId);

            if (!cancellationResult.Succeeded)
            {
                return cancellationResult;
            }

            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
            //purchase = _mapper.Map<Purchase>(purchaseVm);
            _synchronizePurchaseUpdateService.Sync(purchase);

            if (contribution is ContributionOneToOne oneToOne)
            {
                PackagePurchase pkgPurchaseToReomve = oneToOne.PackagePurchases.Where(a => a.TransactionId == payment.TransactionId).FirstOrDefault();
                if (pkgPurchaseToReomve != null)
                {
                    oneToOne.PackagePurchases.Remove(pkgPurchaseToReomve);
                    await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(contributionId, oneToOne);
                    purchase.Payments.Remove(payment);
                    await _unitOfWork.GetGenericRepositoryAsync<Purchase>().Update(purchase.Id, purchase);
                }
            }

            return OperationResult.Success(null);
        }

        public async Task<OperationResult> CancelOneToOneReservation(
            string accountId,
            string contributionId,
            string bookedTimeId,
            DateTime created)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

            if (contribution == null)
            {
                return OperationResult.Failure("Contribution which Id was provided was not found");
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            if (customerStripeAccountId == null)
            {
                _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                return OperationResult.Failure("Stripe customer is not attached to the user");
            }

            if (purchaseVm == null)
            {
                return OperationResult.Failure("Contribution related purchase was not found");
            }

            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (purchaseVm.HasProcessingPayment)
            {
                return OperationResult.Failure(
                    "Unable to cancel purchasing. Contribution payment is in processing. Please try later");
            }

            var payment = purchaseVm.Payments.FirstOrDefault(x =>
                x.PaymentStatus == PaymentStatus.RequiresPaymentMethod && x.HasBookedClassId(bookedTimeId) &&
                x.DateTimeCharged == created);

            if (payment == null)
            {
                return OperationResult.Failure("Contribution related payment was not found");
            }

            if (payment.PaymentOption != PaymentOptions.PerSession)
            {
                return OperationResult.Failure("Only single session purchase can be canceled");
            }

            var cancellationResult = await _stripeService.CancelPaymentIntentAsync(payment.TransactionId);

            if (!cancellationResult.Succeeded)
            {
                return cancellationResult;
            }

            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
            purchase = _mapper.Map<Purchase>(purchaseVm);
            _synchronizePurchaseUpdateService.Sync(purchase);

            return OperationResult.Success(null);
        }

        public async Task<OperationResult<ContributionPaymentIntentDetailsViewModel>> PurchaseOneToOneMonthlySessionSubscriptionAsync(
        string accountId, string contributionId, string paymentMethodId, string couponId)
        {                    
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);
            var contribution = await _contributionRootService.GetOne(contributionId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);

            var account_monthly = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);
            string accountEmail_monthly = account_monthly.Email;
            var cohealerUser_monthly = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var cohealerAccount_monthly = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == cohealerUser_monthly.AccountId);
            var Payment_Option = contribution.PaymentInfo.PaymentOptions.First().ToString();
            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
            {
                Payment_Option = "MonthlySessionSubscription";
            }

            var contributionOwner = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(x => x.Id == contribution.UserId);
            var contributionOneToOne = contribution as ContributionOneToOne;

            var purchaseAmountInCents = contributionOneToOne.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice;
            var serviceProviderIncome = contributionOneToOne.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice; //TransferAmount
            var purchaseGrossAmount = contributionOneToOne.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice; //purchaseGrossAmount

            Coupon coupon = null;
            if(!string.IsNullOrWhiteSpace(couponId))
                coupon = _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(x => x.Id == couponId).Result;

            decimal couponDiscountInPercentage = 1m;
            if (coupon != null)
            {
                if (!string.IsNullOrEmpty(coupon.Id))
                {
                    var validateCouponResult = await _couponService.ValidateByIdAsync(coupon.Id, contribution.Id, PaymentOptions.MonthlySessionSubscription);

                    if (validateCouponResult?.PercentAmount > 0)
                    {
                        couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                    }
                    if (validateCouponResult?.PercentAmount == 100)
                    {
                        var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                        .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == user.Id);
                        var _paymentIntent = new PurchasePayment()
                        {
                            PaymentStatus = PaymentStatus.Succeeded,
                            DateTimeCharged = DateTime.UtcNow,
                            PaymentOption = PaymentOptions.MonthlySessionSubscription,
                            GrossPurchaseAmount = 0,
                            TransferAmount = 0,
                            ProcessingFee = 0,
                            IsInEscrow = !contribution.InvitationOnly,
                            PurchaseCurrency = contribution.DefaultCurrency,
                            Currency = contribution.DefaultCurrency
                        };
                        if (clientPurchase == null)
                        {
                            clientPurchase = new Purchase()
                            {
                                ClientId = user.Id,
                                ContributorId = contribution.UserId,
                                ContributionId = contribution.Id,
                                Payments = new List<PurchasePayment>() { _paymentIntent },
                                SubscriptionId = "-2", // 100% discount subscription
                                ContributionType = contribution.Type,
                                CouponId = couponId,
                                PaymentType = contribution.PaymentType.ToString(),
                                TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
                            };
                        }
                        // todo: check if we need to have a condition here before entering it
                        else
                        {
                            clientPurchase.Payments.Add(_paymentIntent);
                            clientPurchase.CouponId = couponId;
                        }

                        if (clientPurchase.Id is null)
                        {
                            await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
                        }
                        else
                        {
                            _synchronizePurchaseUpdateService.Sync(clientPurchase);
                        }
                        try
                        {
                            AfterSave(contribution, clientPurchase, _paymentIntent, user);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
                        }
                        return OperationResult<ContributionPaymentIntentDetailsViewModel>.Success("100discount");
                    }
                }               
            }

            var validateResult = ValidateRequest(contribution, user);
            if (validateResult.Failed)
            {
                return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(validateResult.Message);
            }

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            string standardAccountId = string.Empty;
            if (contribution.PaymentType == PaymentTypes.Advance && coachUser.IsStandardAccount) standardAccountId = coachUser.StripeStandardAccountId;

            Subscription subscription = null;
            int? paymentSessionLifetimeSeconds = null;

            if (purchaseVm != null)
            {
                if (purchaseVm.HasProcessingPayment)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                        "Contribution payment is in processing. Try later");
                }

                if (purchaseVm.HasUnconfirmedPayment)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                        "You can not use another payment method until there is unconfirmed payment. Cancel your payment instead");
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.PerSession ||
                    purchaseVm.RecentPaymentOption == PaymentOptions.SessionsPackage)
                {
                    //Attempt to find incomplete Payment and then to cancel related Stripe Payment Intent
                    var incompletePayment =
                        purchaseVm.Payments.FirstOrDefault(x => x.PaymentStatus == PaymentStatus.RequiresPaymentMethod);

                    if (incompletePayment != null)
                    {
                        var cancellationResult =
                            await _stripeService.CancelPaymentIntentAsync(incompletePayment.TransactionId, standardAccountId);

                        if (!cancellationResult.Succeeded)
                        {
                            return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(cancellationResult
                                .Message);
                        }
                    }
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.MonthlySessionSubscription
                )  //TODO: need to define logic for this branch
                {
                    if (purchaseVm.Payments.All(x => x.PaymentStatus != PaymentStatus.RequiresPaymentMethod)
                        && purchaseVm.IsPaidAsSubscription)
                    {
                        return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                            "You have already purchased this contribution");
                    }

                    if (purchaseVm.SubscriptionId != null)
                    {
                        var subscriptionResult =
                            await _stripeService.GetProductPlanSubscriptionAsync(purchaseVm.SubscriptionId, standardAccountId);

                        if (!subscriptionResult.Succeeded)
                        {
                            return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                                "Subscription was not found");
                        }

                        subscription = subscriptionResult.Payload;
                        if (subscription.Status != "canceled")
                        {
                            var updatingResult = await _stripeService.UpdateProductPlanSubscriptionPaymentMethodAsync(
                                new UpdatePaymentMethodViewModel
                                {
                                    Id = subscription.Id,
                                    PaymentMethodId = paymentMethodId,
                                },
                                standardAccountId
                            );

                            if (!updatingResult.Succeeded)
                            {
                                return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(updatingResult
                                    .Message);
                            }

                            subscription = updatingResult.Payload;
                        }

                        var paymentIntent = subscription.LatestInvoice.PaymentIntent;

                        if (paymentIntent.Status == PaymentStatus.RequiresPaymentMethod.GetName())
                        {
                            if (subscription.Status == "canceled" &&
                                !purchaseVm.Payments.Exists(x => x.PaymentStatus == PaymentStatus.Succeeded))
                            {
                                var latestInvoice = subscription.LatestInvoice;

                                if (latestInvoice != null && latestInvoice.Status != "draft" &&
                                    latestInvoice.Status != "void")
                                {
                                    var voidResult =
                                        await _stripeService.VoidInvoiceAsync(subscription.LatestInvoiceId, standardAccountId);

                                    if (!voidResult.Succeeded)
                                    {
                                        return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                                            voidResult.Message);
                                    }
                                }
                            }
                            else
                            {
                                var intentUpdateResult = await _stripeService.UpdatePaymentIntentPaymentMethodAsync(
                                    new UpdatePaymentMethodViewModel
                                    { Id = paymentIntent.Id, PaymentMethodId = paymentMethodId }, standardAccountId);

                                if (!intentUpdateResult.Succeeded)
                                {
                                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                                        intentUpdateResult.Message);
                                }
                            }

                            subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(subscription.Id, standardAccountId);

                            if (!subscriptionResult.Succeeded)
                            {
                                return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                                    subscriptionResult.Message);
                            }

                            subscription = subscriptionResult.Payload;
                            paymentIntent = subscription.LatestInvoice.PaymentIntent;
                        }

                        if (paymentIntent.Status == PaymentStatus.Canceled.GetName())
                        {
                            subscription = null;
                        }
                    }
                }
                try
                {
                    purchase = _mapper.Map<Purchase>(purchaseVm);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            var PaymentIntentModel = GetCreatePaymentIntentModel(
                user,
                accountEmail_monthly,
                cohealerAccount_monthly,
                contribution.Id,
                (decimal)purchaseAmountInCents,
                (long)serviceProviderIncome,
                (long)purchaseGrossAmount,
                contributionOwner,
                Payment_Option,
                coupon);

            var subscribeModel = new ProductSubscriptionViewModel
            {
                CustomerId = customerStripeAccountId,
                StripeSubscriptionPlanId = contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId,
                DefaultPaymentMethod = paymentMethodId,
                Iterations = contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration.Value,
                ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                ServiceAgreementType = coachUser.ServiceAgreementType,
                BillingInfo = contribution.PaymentInfo.BillingPlanInfo,
                CouponId = couponId,
                PaymentIntent_Model = PaymentIntentModel,
                StandardAccountId = standardAccountId,
                PaymentType = contribution.PaymentType.ToString(),
            };

            if (subscription is null)
            {
                if (!contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration.HasValue)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                        "Contribution split numbers are not specified");
                }
                                       
                var subscriptionResult =
                    await _stripeService.ScheduleSubscribeToProductPlanAsync(subscribeModel, contribution.Id, PaymentOptions.MonthlySessionSubscription.ToString());

                if (subscriptionResult.Failed)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                        subscriptionResult.Message);
                }

                subscription = subscriptionResult.Payload;

                var invoiceFinalizationResult = await _stripeService.FinalizeInvoiceAsync(subscription.LatestInvoiceId, standardAccountId);

                if (invoiceFinalizationResult.Failed)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(invoiceFinalizationResult
                        .Message);
                }

                subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(subscription.Id, standardAccountId);

                if (subscriptionResult.Failed)
                {
                    return OperationResult<ContributionPaymentIntentDetailsViewModel>.Failure(
                        subscriptionResult.Message);
                }

                subscription = subscriptionResult.Payload;

                if (subscription.LatestInvoice.PaymentIntent.Status != PaymentStatus.Succeeded.GetName())
                {
                    _jobScheduler.ScheduleJob<ISubscriptionCancellationJob>(
                        TimeSpan.FromSeconds(_paymentSessionLifetimeSeconds), subscription.Id, standardAccountId);
                    paymentSessionLifetimeSeconds = _paymentSessionLifetimeSeconds;
                }
            }

            var payment = purchase?.Payments.FirstOrDefault(x =>
                x.TransactionId == subscription.LatestInvoice.PaymentIntentId);

            if (payment is null)
            {
                payment = new PurchasePayment
                {
                    TransactionId = subscription.LatestInvoice.PaymentIntentId,
                    DateTimeCharged = subscription.LatestInvoice.PaymentIntent.Created
                };
            }

            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == cohealerUser.AccountId);
            var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(cohealerAccount.Id);

            payment.PaymentStatus = subscription.LatestInvoice.PaymentIntent.Status.ToPaymentStatusEnum();
            payment.PaymentOption = PaymentOptions.MonthlySessionSubscription;
            payment.GrossPurchaseAmount =
                subscription.LatestInvoice.PaymentIntent.Amount / _stripeService.SmallestCurrencyUnit;
             payment.PurchaseAmount = contribution.PaymentInfo.BillingPlanInfo.BillingPlanPureCost;
            payment.TransferAmount = _pricingCalculationService.CalculateServiceProviderIncomeAsLong( //need to change this...
                (payment.PurchaseAmount * 100),
                contributionOneToOne.PaymentInfo.CoachPaysStripeFee,
                currentPaidTier.PaidTierOption.NormalizedFee,
                contribution.PaymentType, coachUser.CountryId);
            payment.TransferAmount /= 100;
            payment.IsInEscrow = !contribution.InvitationOnly;

            payment.PurchaseCurrency = contribution.DefaultCurrency;
            payment.Currency = contribution.DefaultCurrency;
            payment.TotalCost = contribution.PaymentInfo.BillingPlanInfo.BillingPlanPureCost;

            if (coupon != null)
            {                
                payment.PurchaseAmount = payment.PurchaseAmount * couponDiscountInPercentage;
                payment.TransferAmount = payment.TransferAmount * couponDiscountInPercentage;
                if (subscribeModel.PaymentIntent_Model.CoachPaysFee == true && Payment_Option == "MonthlySessionSubscription")
                {
                    var TransferAmount_Fee = (payment.GrossPurchaseAmount * (subscribeModel.PaymentIntent_Model.Fee / 100) + subscribeModel.PaymentIntent_Model.Fixed);
                    payment.TransferAmount = (decimal)(payment.PurchaseAmount - TransferAmount_Fee);
                    payment.TransferAmount = Math.Round(payment.TransferAmount, 2);
                }
                
            }

            if (purchase is null)
            {
                purchase = new Purchase
                {
                    ClientId = user.Id,
                    ContributorId = contribution.UserId,
                    ContributionId = contributionId,
                    Payments = new List<PurchasePayment> { payment },
                    ContributionType = contribution.Type,
                    SplitNumbers = contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration,
                    PaymentType = contribution.PaymentType.ToString(),
                    TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
                };
                if (coupon != null)
                {                   
                    purchase.CouponId = coupon.Id;
                }                              
            }
            else if (purchase.Payments.All(x => x.TransactionId != subscription.LatestInvoice.PaymentIntentId))
            {
                purchase.Payments.Add(payment);
                if (coupon != null)
                {
                    purchase.CouponId = coupon.Id;
                }
            }

            purchase.SubscriptionId = subscription.Id;

            if (purchase.Id == null)
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(purchase);
            }
            else
            {
                _synchronizePurchaseUpdateService.Sync(purchase);
            }

            var paymentViewModel = new ContributionPaymentIntentDetailsViewModel()
            {
                ClientSecret = subscription.LatestInvoice.PaymentIntent.ClientSecret,
                Currency = contribution.DefaultCurrency,
                Price = subscription.LatestInvoice.PaymentIntent.Amount / _stripeService.SmallestCurrencyUnit,
                Status = subscription.LatestInvoice.PaymentIntent.Status,
                SessionLifeTimeSeconds = paymentSessionLifetimeSeconds,
                Created = subscription.Created,
            };

            return OperationResult<ContributionPaymentIntentDetailsViewModel>.Success(null, paymentViewModel);


            OperationResult ValidateRequest(ContributionBase contribution, User user)
            {
                if (contribution is null || contribution.Status != ContributionStatuses.Approved)
                {
                    return OperationResult.Failure("Contribution which Id was provided was not found");
                }

                if (!(contribution is ContributionOneToOne contributionOneToOne))
                {
                    return OperationResult.Failure(
                        "Unable to purchase one to one package. Contribution which Id was provided is not One-To-One type");
                }

                if (!contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
                {
                    return OperationResult.Failure(
                        $"'{PaymentOptions.MonthlySessionSubscription.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
                }

                var customerStripeAccountId = user.CustomerStripeAccountId;

                if (customerStripeAccountId is null)
                {
                    _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                    return OperationResult.Failure("Stripe customer is not attached to the user");
                }

                return OperationResult.Success(string.Empty);
            }
        }

        //Obsolete Code, this function is no more useable now, just to entertain frontend
        public async Task<OperationResult> OldPurchaseOneToOnePackageAsync(string accountId, string contributionId, string couponId) 
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            if (contribution.PaymentInfo.PackageSessionNumbers == null && contribution.PaymentInfo.MonthlySessionSubscriptionInfo != null && contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
            {
                contribution.PaymentInfo.PackageSessionNumbers = 5;
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            Coupon coupon = null;
            if (!string.IsNullOrWhiteSpace(couponId))
                coupon = _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(x => x.Id == couponId).Result;

            string accountEmail = account.Email;

            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == cohealerUser.AccountId);

            var validationResult = ValidateRequest(contribution, user);

            if (validationResult.Failed)
            {
                return validationResult;
            }

            var contributionOneToOne = contribution as ContributionOneToOne;

            var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(cohealerAccount.Id);

            var Payment_Option = contribution.PaymentInfo.PaymentOptions.First().ToString();
            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage))
            {
                Payment_Option = "SessionsPackage";
            }

            //when 100% off coupon code is applied
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, PaymentOptions.SessionsPackage);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == user.Id);
                    var _paymentIntent = new PurchasePayment()
                    {
                        PaymentStatus = PaymentStatus.Succeeded,
                        DateTimeCharged = DateTime.UtcNow,
                        PaymentOption = PaymentOptions.SessionsPackage,
                        GrossPurchaseAmount = 0,
                        TransferAmount = 0,
                        ProcessingFee = 0,
                        IsInEscrow = !contribution.InvitationOnly,
                        PurchaseCurrency = contribution.DefaultCurrency,
                        Currency = contribution.DefaultCurrency,
                        TransactionId = "100_off_" + Guid.NewGuid().ToString()
                    };
                    //also update the purchase package list in contribution


                    if (clientPurchase == null)
                    {
                        clientPurchase = new Purchase()
                        {
                            ClientId = user.Id,
                            ContributorId = contribution.UserId,
                            ContributionId = contribution.Id,
                            Payments = new List<PurchasePayment>() { _paymentIntent },
                            SubscriptionId = "-2", // 100% discount subscription
                            ContributionType = contribution.Type,
                            CouponId = couponId
                        };
                    }
                    // todo: check if we need to have a condition here before entering it
                    else
                    {
                        clientPurchase.Payments.Add(_paymentIntent);
                        clientPurchase.CouponId = couponId;
                    }

                    if (clientPurchase.Id is null)
                    {
                        await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
                    }
                    else
                    {
                        _synchronizePurchaseUpdateService.Sync(clientPurchase);
                    }
                    contributionOneToOne.PackagePurchases.Add(new PackagePurchase
                    {
                        TransactionId = _paymentIntent.TransactionId,
                        UserId = user.Id,
                        SessionNumbers = contributionOneToOne.PaymentInfo.PackageSessionNumbers.Value,
                        IsConfirmed = true
                    });
                    await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);
                    try
                    {
                        AfterSave(contribution, clientPurchase, _paymentIntent, user);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
                    }
                    return OperationResult<string>.Success("100discount");
                }
            }

            var CreateOrReusePaymentIntentResult =
                await CreateOrReusePaymentIntent(user, accountEmail, cohealerAccount, currentPaidTier.PaidTierOption, contributionOneToOne, coupon);

            if (CreateOrReusePaymentIntentResult.Failed)
            {
                return CreateOrReusePaymentIntentResult;
            }

            var (contributionOwner, payment, purchase, paymentIntent) = CreateOrReusePaymentIntentResult.Payload;

            payment.PurchaseCurrency = contribution.DefaultCurrency;
            payment.Currency = contribution.DefaultCurrency;
            if (purchase == null)
            {
                //Means that there is the first authorized Client request for current contribution purchasing
                purchase = new Purchase
                {
                    ClientId = user.Id,
                    ContributorId = contributionOwner.Id,
                    ContributionId = contributionOneToOne.Id,
                    Payments = new List<PurchasePayment> { payment },
                    ContributionType = contributionOneToOne.Type,
                };
                if (coupon != null)
                {
                    purchase.CouponId = coupon.Id;
                }
            }
            else if (purchase.Payments.All(x => x.TransactionId != payment.TransactionId))
            {
                purchase.Payments.Add(payment);
                if (coupon != null)
                {
                    purchase.CouponId = coupon.Id;
                }
            }

            if (purchase.Id != null)
            {
                _synchronizePurchaseUpdateService.Sync(purchase);
            }
            else
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(purchase);
            }

            var paymentViewModel = new ContributionPaymentIntentDetailsViewModel
            {
                ClientSecret = paymentIntent.ClientSecret,
                Currency = contribution.DefaultCurrency,
                Price = payment.GrossPurchaseAmount,
                PlatformFee = payment.GrossPurchaseAmount - payment.PurchaseAmount,
                Status = paymentIntent.Status,
                SessionLifeTimeSeconds = _paymentSessionLifetimeSeconds,
                Created = paymentIntent.Created,
            };

            return OperationResult.Success(null, paymentViewModel);

            OperationResult ValidateRequest(ContributionBase contribution, User user)
            {
                if (contribution == null || contribution.Status != ContributionStatuses.Approved)
                {
                    return OperationResult.Failure("Contribution which Id was provided was not found");
                }

                if (!(contribution is ContributionOneToOne contributionOneToOne))
                {
                    return OperationResult.Failure(
                        "Unable to purchase one to one package. Contribution which Id was provided is not One-To-One type");
                }

                if (contributionOneToOne.PackagePurchases.Exists(p =>
                    p.UserId == user.Id && !p.IsCompleted && p.IsConfirmed))
                {
                    return OperationResult.Failure(
                        "Unable to purchase sessions package until you have incomplete package");
                }

                if (!contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage) && !contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
                {
                    return OperationResult.Failure(
                        $"'{PaymentOptions.SessionsPackage.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
                }

                if (!contribution.PaymentInfo.PackageSessionNumbers.HasValue)
                {
                    return OperationResult.Failure("One to one package session numbers are not specified");
                }

                var customerStripeAccountId = user.CustomerStripeAccountId;

                if (customerStripeAccountId == null)
                {
                    _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                    return OperationResult.Failure("Stripe customer is not attached to the user");
                }

                return OperationResult.Success(string.Empty);
            }
        }

        public async Task<OperationResult> PurchaseOneToOnePackageAsync(string accountId, string contributionId, string couponId, string accessCode)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            if (contribution.PaymentInfo.PackageSessionNumbers == null && contribution.PaymentInfo.MonthlySessionSubscriptionInfo != null && contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
            {
                contribution.PaymentInfo.PackageSessionNumbers = 5;
            }

            var contributionOneToOne = contribution as ContributionOneToOne;
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            Coupon coupon = null;
            if (!string.IsNullOrWhiteSpace(couponId))
                coupon = _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(x => x.Id == couponId).Result;

            string accountEmail = account.Email;

            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == cohealerUser.AccountId);

            var validationResult = ValidateRequest(contribution, user);

            if (validationResult.Failed)
            {
                return validationResult;
            }

            if (contribution.PaymentType == PaymentTypes.Advance && (!cohealerUser.IsStandardAccount || string.IsNullOrEmpty(cohealerUser.StripeStandardAccountId)))
            {
                return OperationResult.Failure("unsupported payment type for contribtuion", "unsupported payment type for contribtuion. Advance payment is enable for the Stripe standard account only");
            }

            string standardAccountId = string.Empty;
            if (contribution.PaymentType == PaymentTypes.Advance) standardAccountId = cohealerUser.StripeStandardAccountId;

            var Payment_Option = contribution.PaymentInfo.PaymentOptions.First();

            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage) || contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.FreeSessionsPackage))
            {
                Payment_Option = PaymentOptions.SessionsPackage;
            }
            else
            {
                return OperationResult.Failure("unsupported payment option. The contribution is not available as session package");
            }

            //when 100% off coupon code is applied
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, PaymentOptions.SessionsPackage);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var purchaseResult = await PurchaseSessionPackageFreeWithoutCheckout(contributionOneToOne, user.Id, couponId);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased Free session package with 100% off coupon", purchaseResult.Payload);
                    }
                    return OperationResult.Failure(purchaseResult.Message);
                }
            }
            //when joing the course with free access link
            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.FreeSessionsPackage))
            {
                var isAccessCodeValid = validateAccessCode(accessCode, contributionId);
                if (isAccessCodeValid)
                {
                    var purchaseResult = await PurchaseSessionPackageFreeWithoutCheckout(contributionOneToOne, user.Id, couponId);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased Free session package with Access Code", purchaseResult.Payload);
                    }
                    return OperationResult.Failure(purchaseResult.Message);
                }
            }

            var priceResult = await GetPriceForProductPaymentOptionAsync(contribution, Payment_Option, couponId);
            if (priceResult.Failed)
            {
                return priceResult;
            }
            var (priceId, cost) = priceResult.Payload;

            //fee calculation
            var country = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == cohealerUser.CountryId);
            var dynamicStripeFee = await _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Alpha2Code);
            var paymentInfo = contribution.PaymentInfo;

            var sessionModel = new CreateCheckoutSessionModel()
            {
                TotalChargedCost = cost,
                StripeFee = dynamicStripeFee?.Fee ?? 2.9M,
                FixedStripeAmount = dynamicStripeFee?.Fixed ?? 0.30M,
                InternationalFee = dynamicStripeFee?.International ?? 3.9M,
                ProductCost = paymentInfo.PackageCost.HasValue ? paymentInfo.PackageCost.Value : paymentInfo.Cost.Value * paymentInfo.PackageSessionNumbers.Value,
                DiscountPercent = contribution.PaymentInfo.PackageSessionDiscountPercentage,
                CoachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee,
                ServiceAgreementType = cohealerUser.ServiceAgreementType,
                StripeCustomerId = user.CustomerStripeAccountId,
                ContributionId = contribution.Id,
                PaymentOption = Payment_Option,
                PurchaseId = null,
                PriceId = priceId,
                CouponId = couponId,
                ConnectedStripeAccountId = cohealerUser.ConnectedStripeAccountId,
                StripeStandardAccountId = cohealerUser.StripeStandardAccountId,
                IsStandardAccount = cohealerUser.IsStandardAccount,
                paymentType = contribution.PaymentType,
                ClientFirstName = user.FirstName,
                ClientLastName = user.LastName,
                ClientEmail = account.Email,
                CoachEmail = cohealerAccount.Email,
                ContributionTitle = contribution.Title,
                TaxType = contribution.TaxType
            };


            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, Payment_Option);
                sessionModel.CouponPerecent = validateCouponResult?.PercentAmount;
            }

            var result = await _stripeService.CreateCheckoutSessionSinglePayment(sessionModel);
            if (result.Succeeded)

            {
                if (contribution.PaymentType == PaymentTypes.Advance)
                {
                    return OperationResult<string>.Success(String.Empty, (string)result.Payload.RawJObject["url"]);
                }
                return OperationResult.Success(String.Empty, result.Payload.Id);
            }
            else
            {
                return OperationResult<string>.Failure(result.Message);
            }

            OperationResult ValidateRequest(ContributionBase contribution, User user)
            {
                if (contribution == null || contribution.Status != ContributionStatuses.Approved)
                {
                    return OperationResult.Failure("Contribution which Id was provided was not found");
                }

                if (!(contribution is ContributionOneToOne contributionOneToOne))
                {
                    return OperationResult.Failure(
                        "Unable to purchase one to one package. Contribution which Id was provided is not One-To-One type");
                }

                if (contributionOneToOne.PackagePurchases.Exists(p =>
                    p.UserId == user.Id && !p.IsCompleted && p.IsConfirmed))
                {
                    return OperationResult.Failure(
                        "Unable to purchase sessions package until you have incomplete package");
                }

                if (!contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage) && !contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription) &&
                    !contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free) && !contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.FreeSessionsPackage))
                {
                    return OperationResult.Failure(
                        $"'{PaymentOptions.SessionsPackage.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
                }

                if (!contribution.PaymentInfo.PackageSessionNumbers.HasValue)
                {
                    return OperationResult.Failure("One to one package session numbers are not specified");
                }

                var customerStripeAccountId = user.CustomerStripeAccountId;

                if (customerStripeAccountId == null)
                {
                    _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                    return OperationResult.Failure("Stripe customer is not attached to the user");
                }

                return OperationResult.Success(string.Empty);
            }
        }
        private class CreateOrReusePaymentIntentResult
        {
            public User ContributionOwner { get; set; }

            public PurchasePayment Payment { get; set; }

            public Purchase Purchase { get; set; }

            public PaymentIntent PaymentIntent { get; set; }

            public decimal PurchaseAmount { get; set; }

            public long PurchaseGrossAmount { get; set; }

            public long ServiceProviderIncome { get; set; }

            public void Deconstruct(out User contributionOwner, out PurchasePayment payment, out Purchase purchase,
                out PaymentIntent paymentIntent)
            {
                contributionOwner = ContributionOwner;
                payment = Payment;
                purchase = Purchase;
                paymentIntent = PaymentIntent;
            }
        }

        private async Task<OperationResult<CreateOrReusePaymentIntentResult>> CreateOrReusePaymentIntent(User user,
            string accountEmail, Account cohealerAccount, PaidTierOption paidTierOption, ContributionOneToOne contributionOneToOne, Coupon coupon)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x =>
                x.Id == contributionOneToOne.Id);
            

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(x =>
                x.ContributionId == contributionOneToOne.Id && x.ClientId == user.Id);

            var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x =>
                x.Id == contributionOneToOne.UserId);

            var getPaymentResult = GetReusablePaymentByPaymentOption(purchase, PaymentOptions.SessionsPackage, contribution);

            if (getPaymentResult.Failed)
            {
                return OperationResult<CreateOrReusePaymentIntentResult>.Failure(getPaymentResult.Message);
            }

            var payment = getPaymentResult.Payload;
            var purchaseAmountInCents = CalculatePackagePurchaseAmount(contributionOneToOne); //PurchaseAmount
            var serviceProviderIncome= _pricingCalculationService.CalculateServiceProviderIncomeAsLong(
                purchaseAmountInCents,
                contributionOneToOne.PaymentInfo.CoachPaysStripeFee,
                paidTierOption.NormalizedFee,
                contribution.PaymentType, contributor.CountryId); //TransferAmount
            var purchaseGrossAmount= _calculationFeeService.CalculateGrossAmountAsLong(
                purchaseAmountInCents,
                contributionOneToOne.PaymentInfo.CoachPaysStripeFee, contributionOneToOne.UserId); //purchaseGrossAmount
            
            var Payment_Option = contributionOneToOne.PaymentInfo.PaymentOptions.First().ToString();
            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage))
            {
                Payment_Option = "SessionsPackage";
            }
            var contributionOwner = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(x => x.Id == contributionOneToOne.UserId);

            var model = GetCreatePaymentIntentModel(
                user,
                accountEmail,
                cohealerAccount,
                contributionOneToOne.Id,
                purchaseAmountInCents,
                serviceProviderIncome,
                purchaseGrossAmount,
                contributionOwner,
                Payment_Option,
                coupon);
            decimal couponDiscountInPercentage = 1m;
            if (coupon != null)
            {
                if (!string.IsNullOrEmpty(coupon.Id))
                {
                    var validateCouponResult = await _couponService.ValidateByIdAsync(coupon.Id, contribution.Id, PaymentOptions.SessionsPackage);
                    if (validateCouponResult?.PercentAmount > 0)
                    {
                        couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                    }
                }               
                //with coupon
                purchaseAmountInCents = purchaseAmountInCents * couponDiscountInPercentage;
                serviceProviderIncome = (long)(serviceProviderIncome * couponDiscountInPercentage);
                               
                if (contributionOneToOne.PaymentInfo.CoachPaysStripeFee==true && Payment_Option== "SessionsPackage")
                {
                    decimal purchaseAmountInCents_temp = purchaseAmountInCents;
                    var calculatedFee= (((purchaseAmountInCents_temp / 100) * (model.Fee / 100)) + model.Fixed);
                    decimal calculatedTransferAmount = ((decimal)((purchaseAmountInCents_temp / 100)- calculatedFee));
                    serviceProviderIncome = (long)(calculatedTransferAmount * 100);
                }
                
                purchaseGrossAmount = _calculationFeeService.CalculateGrossAmountAsLong(
                purchaseAmountInCents,
                contributionOneToOne.PaymentInfo.CoachPaysStripeFee, contributionOneToOne.UserId); //purchaseGrossAmount
            }

            model = GetCreatePaymentIntentModel(
               user,
               accountEmail,
               cohealerAccount,
               contributionOneToOne.Id,
               purchaseAmountInCents,
               serviceProviderIncome,
               purchaseGrossAmount,
               contributionOwner,
               Payment_Option,
               coupon);

            PaymentIntent paymentIntent = null;

            if (payment != null)
            {
                if (!contributionOneToOne.PackagePurchases.Exists(p => p.TransactionId == payment.TransactionId))
                {
                    return OperationResult<CreateOrReusePaymentIntentResult>.Failure(
                        "Payment related package of sessions was not found");
                }

                //Trying to REUSE existing Incomplete Stripe Payment Intent
                paymentIntent = await _stripeService.GetPaymentIntentAsync(payment.TransactionId);
                //Updating existing Payment Intent with new calculated contribution Amount
                PaymentIntentUpdateViewModel updateViewModel = new PaymentIntentUpdateViewModel {
                    Id = paymentIntent.Id,
                    Amount = purchaseGrossAmount
                };
                if (contributor.IsBetaUser)
                {
                    updateViewModel.TransferAmount = serviceProviderIncome;
                    updateViewModel.ConnectedAccountId = contributor.ConnectedStripeAccountId;
                }

                var paymentIntentResult = await _stripeService.UpdatePaymentIntentAsync(updateViewModel);

                if (paymentIntentResult.Failed)
                {
                    return OperationResult<CreateOrReusePaymentIntentResult>.Failure(paymentIntentResult.Message);
                }

                paymentIntent = paymentIntentResult.Payload;
            }
            else
            {
                //Creating new Payment Intent if any incomplete existing was not found
                var paymentIntentResult = await _stripeService.CreatePaymentIntentAsync(model, contribution.DefaultCurrency, contributor.ConnectedStripeAccountId);

                if (paymentIntentResult.Failed)
                {
                    return OperationResult<CreateOrReusePaymentIntentResult>.Failure(paymentIntentResult.Message);
                }

                paymentIntent = paymentIntentResult.Payload;

                if (paymentIntent.Status != PaymentStatus.Succeeded.GetName())
                {
                    //Payment Intent cancellation job scheduling
                    _jobScheduler.ScheduleJob<IPaymentCancellationJob>(
                        TimeSpan.FromSeconds(_paymentSessionLifetimeSeconds), paymentIntent.Id);
                }

                //Means that there is the first request for current contribution purchasing
                //Creating new local payment entry
                payment = new PurchasePayment
                {
                    TransactionId = paymentIntent.Id,
                    DateTimeCharged = paymentIntent.Created
                };
            }

            payment.PaymentStatus = paymentIntent.Status.ToPaymentStatusEnum();
            payment.PaymentOption = PaymentOptions.SessionsPackage;
            payment.TransferAmount = serviceProviderIncome / _stripeService.SmallestCurrencyUnit;
            payment.PurchaseAmount = purchaseAmountInCents / _stripeService.SmallestCurrencyUnit;
            payment.GrossPurchaseAmount = purchaseGrossAmount / _stripeService.SmallestCurrencyUnit;
            payment.IsInEscrow = !contributionOneToOne.InvitationOnly;
            if (contributionOneToOne.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SessionsPackage))//session package
            {
                if (contributionOneToOne.PaymentInfo.PackageCost.HasValue)
                {
                    payment.TotalCost = (decimal)contributionOneToOne.PaymentInfo.PackageCost;
                }
                else
                {
                    payment.TotalCost = (decimal)contributionOneToOne.PaymentInfo.Cost;
                    payment.TotalCost = payment.TotalCost * (decimal)contributionOneToOne.PaymentInfo.PackageSessionNumbers;
                }
                
            }
            else if (contributionOneToOne.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription)) //monthly package
            {
                payment.TotalCost = (decimal)contributionOneToOne.PaymentInfo.BillingPlanInfo.TotalBillingPureCost;
            }
                        
            await SetAffiliateIncome(payment, purchaseAmountInCents, cohealerAccount, paidTierOption.NormalizedFee);

            var result = new CreateOrReusePaymentIntentResult()
            {
                ContributionOwner = contributionOwner,
                Payment = payment,
                Purchase = purchase,
                PaymentIntent = paymentIntent,
            };

            return OperationResult<CreateOrReusePaymentIntentResult>.Success(string.Empty, result);

            OperationResult<PurchasePayment> GetReusablePaymentByPaymentOption(Purchase purchase,
                PaymentOptions paymentOption, ContributionBase contribution)
            {
                PurchasePayment payment = null;
                var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
                var contributionAndStandardAccountIdDic =  _commonService.GetStripeStandardAccounIdFromContribution(contribution).GetAwaiter().GetResult();
                purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

                if (purchaseVm != null)
                {
                    if (purchaseVm.HasProcessingPayment)
                    {
                        return OperationResult<PurchasePayment>.Failure(
                            "Contribution payment is in processing. Try later");
                    }

                    if (purchaseVm.HasUnconfirmedPayment)
                    {
                        return OperationResult<PurchasePayment>.Failure(
                            "Unable to purchase sessions package until you have unconfirmed payments");
                    }

                    //Attempt to find incomplete Payment and then to REUSE related Stripe Payment Intent
                    payment = purchaseVm.Payments.FirstOrDefault(x =>
                        x.PaymentStatus == PaymentStatus.RequiresPaymentMethod && x.PaymentOption == paymentOption);
                }

                return OperationResult<PurchasePayment>.Success(string.Empty, payment);
            }

            decimal CalculatePackagePurchaseAmount(ContributionOneToOne contributionOneToOne)
            {
                decimal purchaseAmount = 0;

                //Calculates Contribution Cost in the smallest currency unit
                if (contributionOneToOne.PaymentInfo.PackageCost != null)
                {
                    purchaseAmount =
                      contributionOneToOne.PaymentInfo.PackageCost.Value * _stripeService.SmallestCurrencyUnit;
                }
                else if (contributionOneToOne.PaymentInfo.Cost != null)
                {
                    purchaseAmount =
                     contributionOneToOne.PaymentInfo.Cost.Value *
                     contributionOneToOne.PaymentInfo.PackageSessionNumbers.Value * _stripeService.SmallestCurrencyUnit;
                }
                else
                {
                    purchaseAmount =
                      contributionOneToOne.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice.Value
                      * (decimal)contributionOneToOne.PaymentInfo.MonthlySessionSubscriptionInfo.Duration
                      * _stripeService.SmallestCurrencyUnit;
                }

                if (contributionOneToOne.PaymentInfo.PackageSessionDiscountPercentage.HasValue)
                {
                    //Applies package discount
                    purchaseAmount -= purchaseAmount * contributionOneToOne.PaymentInfo.PackageSessionDiscountPercentage.Value / 100;
                }

                return purchaseAmount;
            }
        }

       private bool validateAccessCode(string accessCode, string contributionId)
        {
            if (string.IsNullOrEmpty(accessCode) || string.IsNullOrEmpty(contributionId))
            {
                return false;
            }

            var accessCodeModel =
                 _unitOfWork.GetRepositoryAsync<AccessCode>()
                    .GetOne(e => e.Code == accessCode && e.ContributionId == contributionId).GetAwaiter().GetResult();

            if (accessCodeModel is null)
            {
                return false;
            }

            if (accessCodeModel.ValidTill < DateTime.UtcNow)
            {
                return false;
            }
            return true;
        }

        private async Task<OperationResult> BookSingleSessionForFree(BookOneToOneTimeViewModel model, ContributionOneToOne contributionOneToOne, string requesterAccountId, User user)
        {
            var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
            .GetOne(x => x.ContributionId == contributionOneToOne.Id && x.ClientId == user.Id);
            var bookingResult = await BookOneToOneTimeAsync(model, requesterAccountId);
            if (bookingResult.Failed)
            {
                return OperationResult<string>.Failure(bookingResult.Message);
            }
            var bookedTimes = (BookOneToOneTimeResultViewModel)bookingResult.Payload;
            var bookedClassesIds = bookedTimes.AvailabilityTimeIdBookedTimeIdPairs.SelectMany(t => t.Value).ToList();
            var payment100Off = new PurchasePayment()
            {
                TransactionId = "100_off_" + Guid.NewGuid().ToString(),
                PaymentStatus = PaymentStatus.Succeeded,
                DateTimeCharged = DateTime.UtcNow,
                PaymentOption = PaymentOptions.PerSession,
                GrossPurchaseAmount = 0.0m,
                TransferAmount = 0.0m,
                ProcessingFee = 0.0m,
                IsInEscrow = !contributionOneToOne.InvitationOnly,
                BookedClassesIds = bookedClassesIds,
                PurchaseCurrency = contributionOneToOne.DefaultCurrency,
                Currency = contributionOneToOne.DefaultCurrency
            };
            if (clientPurchase == null)
            {
                clientPurchase = new Purchase()
                {
                    ClientId = user.Id,
                    ContributorId = contributionOneToOne.UserId,
                    ContributionId = contributionOneToOne.Id,
                    Payments = new List<PurchasePayment>() { payment100Off },
                    SubscriptionId = "-2", // 100% discount subscription
                    ContributionType = contributionOneToOne.Type,
                    CouponId = model.CouponId,
                    PaymentType = contributionOneToOne.PaymentType.ToString(),
                    TaxType = contributionOneToOne.PaymentType == PaymentTypes.Advance ? contributionOneToOne.TaxType.ToString() : string.Empty
                };
            }
            // todo: check if we need to have a condition here before entering it
            else
            {
                clientPurchase.Payments.Add(payment100Off);
                clientPurchase.CouponId = model.CouponId;
            }
            if (clientPurchase.Id is null)
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
            }
            else
            {
                _synchronizePurchaseUpdateService.Sync(clientPurchase);
            }
            var metadata = new Dictionary<string, string>();
            metadata.Add(Constants.Contribution.Payment.AvailabilityTimeIdBookedTimeIdPairsKey, JsonConvert.SerializeObject(bookedTimes.AvailabilityTimeIdBookedTimeIdPairs));
            var fakePaymentIntane = new PaymentIntent()
            {
                Metadata = metadata
            };
            // fetch contribution again with updated data
            contributionOneToOne = await _contributionRootService.GetOne(contributionOneToOne.Id) as ContributionOneToOne;
            var oneToOneHandleResult =
            HandleOneToOneContributionPurchaseEvent(contributionOneToOne, fakePaymentIntane, payment100Off, user);
            try
            {
                await _fcmService.SendFreeOneToOneContributionJoinPushNotification(contributionOneToOne.Id, user.Id);
            }
            catch
            {

            }
            return OperationResult<string>.Success("100discount", "Free session joined successfully.");
        }

        public async Task<OperationResult> PurchaseOneToOneContributionAsync(
            string requesterAccountId,
            BookOneToOneTimeViewModel model)
        {
            var contribution = await _contributionRootService.GetOne(model.ContributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == requesterAccountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == requesterAccountId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            if (contribution == null || contribution.Status != ContributionStatuses.Approved)
            {
                return OperationResult<string>.Failure("Contribution which Id was provided was not found");
            }

            if (!(contribution is ContributionOneToOne contributionOneToOne))
            {
                return OperationResult<string>.Failure(
                    "Unable to book one to one time. Contribution which Id was provided is not One-To-One type");
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            if (customerStripeAccountId == null)
            {
                _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                return OperationResult<string>.Failure("Stripe customer is not attached to the user");
            }

            if (contributionOneToOne.PackagePurchases.Exists(p => p.UserId == user.Id && !p.IsConfirmed)
                && !contributionOneToOne.PackagePurchases.Exists(c => c.UserId == user.Id && c.IsConfirmed))
            {
                return OperationResult<string>.Failure(
                    "Unable to book one to one time. You have unconfirmed session package payment");
            }

            var package =
                contributionOneToOne.PackagePurchases.FirstOrDefault(p =>
                    p.UserId == user.Id && !p.IsCompleted && p.IsConfirmed);

            if (package != null && package.FreeSessionNumbers < 1)
            {
                return OperationResult<string>.Failure(
                    $"Unable to book one to one time. The are only {package.FreeSessionNumbers} free sessions left in the package you bought");
            }


            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == model.ContributionId && x.ClientId == user.Id);

            var cohealer = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);

            if (package != null)
            {
                var bookingResult = await BookOneToOneTimeAsync(model, requesterAccountId);

                if (bookingResult.Failed)
                {
                    return OperationResult<string>.Failure(bookingResult.Message);
                }

                var bookedTimes = (BookOneToOneTimeResultViewModel)bookingResult.Payload;
                var bookedClassesIds = bookedTimes.AvailabilityTimeIdBookedTimeIdPairs.SelectMany(t => t.Value).ToList();


                contribution = await _contributionRootService.GetOne(model.ContributionId);

                contributionOneToOne = contribution as ContributionOneToOne;
                package = contributionOneToOne.PackagePurchases.First(p => p.TransactionId == package.TransactionId);
                var packagePayment = purchase.Payments.Single(pm => pm.TransactionId == package.TransactionId);

                var bookedTimePairs = contributionOneToOne.AvailabilityTimes.Join(
                    bookedTimes.AvailabilityTimeIdBookedTimeIdPairs, x => x.Id, y => y.Key,
                    (x, y) => new { AllBookedTimes = x.BookedTimes, BookedTimes = y.Value });

                foreach (var bookedTimePair in bookedTimePairs)
                {
                    foreach (var bookedTime in bookedTimePair.AllBookedTimes.Where(t =>
                        bookedTimePair.BookedTimes.Contains(t.Id)))
                    {
                        bookedTime.IsPurchaseConfirmed = true;

                        var note = new Note
                        {
                            UserId = user.Id,
                            ClassId = bookedTime.Id,
                            ContributionId = contribution.Id,
                            Title = $"Session {bookedTime.SessionIndex}",
                        };

                        _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();

                        note.Title = $"Session {bookedTime.SessionIndex}";
                        note.UserId = cohealer.Id;
                        note.Id = null;

                        _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();
                    }
                }

                foreach (var bookedTime in bookedTimes.AvailabilityTimeIdBookedTimeIdPairs)
                {
                    if (package.AvailabilityTimeIdBookedTimeIdPairs.TryGetValue(bookedTime.Key,
                        out var existingBookedTimes))
                    {
                        existingBookedTimes.AddRange(bookedTime.Value);
                    }
                    else
                    {
                        package.AvailabilityTimeIdBookedTimeIdPairs.Add(bookedTime.Key, bookedTime.Value.ToList());
                    }
                }

                packagePayment.BookedClassesIds.AddRange(bookedClassesIds);

                await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                    .Update(contributionOneToOne.Id, contributionOneToOne);
                _synchronizePurchaseUpdateService.Sync(purchase);

                await NotifyClientAndCoachAboutBookedSessions($"{user.FirstName} {user.LastName}", user.Id,
                    contributionOneToOne, bookedClassesIds);

                return OperationResult<PackagePaymentDetailViewModel>.Success(_mapper.Map<PackagePaymentDetailViewModel>(package));
            }

            if (!IsPerSessionAllowed(contribution))
            {
                return OperationResult<string>.Failure(
                    $"{contribution.Title} is available as a package of sessions. Please purchase a package prior to selecting your session time(s).");
            }

            if (model.CreateSingleSession)
            {

                // check if 100% coupon code applies here 
                if (model.CouponId != null)
                {
                    var validateCouponResult = await _couponService.ValidateByIdAsync(model.CouponId, model.ContributionId, PaymentOptions.PerSession);
                    if (validateCouponResult?.PercentAmount == 100)
                    {
                        var bookSessionResult = await BookSingleSessionForFree(model, contributionOneToOne, requesterAccountId, user);
                        if (bookSessionResult.Succeeded)
                        {
                            return OperationResult.Success("Single booked through 100% off coupon", bookSessionResult.Payload);
                        }
                    }
                }

                // check if session payment option is free
                if(model.PaymentOption == PaymentOptions.Free.ToString())
                {
                    var isAccessCodeValid = validateAccessCode(model.AccessCode, contribution.Id);
                    if (isAccessCodeValid)
                    {
                        var bookSessionResult = await BookSingleSessionForFree(model, contributionOneToOne, requesterAccountId, user);
                        if (bookSessionResult.Succeeded)
                        {
                            return OperationResult.Success("Single Booked Free Session with Access Code", bookSessionResult.Payload);
                        }
                    }
                }


                var result = await PurchaseOneToOneWithCheckout(contribution.Id, null/*bookedTimes.AvailabilityTimeIdBookedTimeIdPairs*/, purchase?.Id,
                    requesterAccountId, PaymentOptions.PerSession, model.CouponId, model.AvailabilityTimeId, model);

                if (result.Failed)
                {
                    return result;
                }

                string resultPayload = null;
                if (result.Payload is Stripe.Checkout.Session sessionResult)
                {
                    //payment.TransactionId = sessionResult.PaymentIntentId;
                    resultPayload = sessionResult.Id;
                }
                else
                {
                    resultPayload = (string)result.Payload;
                }

                return OperationResult<string>.Success(resultPayload);
            }
            else
            {
                return OperationResult<string>.Success("test");
            }
        }

        private bool IsPerSessionAllowed(ContributionBase contribution)
        {
            return contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.PerSession)
                    || contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription)
                    || contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free);
        }

        private async Task NotifyClientAndCoachAboutBookedSessions(string clientName, string clientId,
            ContributionOneToOne contributionOneToOne, List<string> bookedClassesIds)
        {
            var locationUrl = contributionOneToOne.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(contributionOneToOne.Id));
            try
            {
                var availabilityTimes = contributionOneToOne.GetAvailabilityTimes(clientName);
                var bookedAvailabilityTimes = availabilityTimes.Keys.Where(bookedClassesIds.Contains)
                    .Select(e => availabilityTimes[e]).ToList();
                //await _notificationService.SendOneToOneCourseSessionBookedNotificationToCoachAsync(
                //    contributionOneToOne.Title, contributionOneToOne.UserId, locationUrl, bookedAvailabilityTimes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during sending book session time emails for coach");
            }

            try
            {
                var availabilityTimes = contributionOneToOne.GetAvailabilityTimes(string.Empty);
                var bookedAvailabilityTimes = availabilityTimes.Keys.Where(bookedClassesIds.Contains)
                    .Select(e => availabilityTimes[e]).ToList();
                //Send Nylas Event
                bool sendIcalAttachment = true;
                try
                {                  
                    var updated = _mapper.Map<ContributionBase>(contributionOneToOne);

                    //Nylas Event creation if External calendar is attached
                    var coach = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contributionOneToOne.UserId);
                    if (!string.IsNullOrWhiteSpace(contributionOneToOne.ExternalCalendarEmail))
                    {
                        var NylasAccount = await _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == coach.AccountId && n.EmailAddress.ToLower() == contributionOneToOne.ExternalCalendarEmail.ToLower());
                        if (NylasAccount != null && !string.IsNullOrEmpty(contributionOneToOne.ExternalCalendarEmail))
                        {
                            if (!string.IsNullOrEmpty(NylasAccount.CalendarId))
                            {
                                foreach (BookedTimeToAvailabilityTime bookedTimeToAvailabilityTime in bookedAvailabilityTimes)
                                {
                                    CalendarEvent calevent = _mapper.Map<CalendarEvent>(bookedTimeToAvailabilityTime);
                                    calevent.Location = locationUrl;
                                    calevent.Description = contributionOneToOne.CustomInvitationBody;
                                    NylasEventCreation eventResponse = await _notificationService.CreateorUpdateCalendarEvent(calevent, clientId, NylasAccount, bookedTimeToAvailabilityTime);
                                    // bookedTimeToAvailabilityTime.BookedTime.CalendarEventID = eventResponse.id;
                                    // bookedTimeToAvailabilityTime.BookedTime.CalendarId = eventResponse.calendar_id;
                                    EventInfo eventInfo = new EventInfo()
                                    {
                                        CalendarEventID = eventResponse.id,
                                        CalendarId = eventResponse.calendar_id,
                                        NylasAccountId = eventResponse.account_id,
                                        AccessToken = NylasAccount.AccessToken,
                                        ParticipantId = clientId
                                    };
                                    bookedTimeToAvailabilityTime.BookedTime.EventInfo = eventInfo;

                                }
                                sendIcalAttachment = false;
                                var ucourse = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(updated.Id, updated);
                            }
                        }
                    }
                }
                    
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error during sending one to one Nylas Invite to client/coach");

                }

                try
                {
                   
                    await _notificationService.SendOneToOneCourseSessionBookedNotificationToCoachAsync(contributionOneToOne.Id,
                        contributionOneToOne.Title, contributionOneToOne.UserId, locationUrl, bookedAvailabilityTimes,contributionOneToOne.CustomInvitationBody, sendIcalAttachment);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during sending book session time emails for coach");
                }

                await _notificationService.SendOneToOneCourseSessionBookedNotificationToClientAsync(contributionOneToOne.Id,
                    contributionOneToOne.Title, clientId, locationUrl, bookedAvailabilityTimes, contributionOneToOne.CustomInvitationBody, sendIcalAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during sending book session time emails for client");
            }
        }

        public async Task<OperationResult> PurchaseEntireCourseContributionAsync(
            string accountId,
            string contributionId,
            string couponId)
        {

            //await _emailService.SendAsync("uzair@cohere.live", "PurchaseEntireCourseContributionAsync function starts", "") 
            var contribution = await _contributionRootService.GetOne(contributionId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            String accountEmail = account.Email;
            Coupon coupon = null;
            if (!string.IsNullOrWhiteSpace(couponId))
                coupon = _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(x => x.Id == couponId).Result;
           
            if (contribution is null || contribution.Status != ContributionStatuses.Approved)
            {
                return OperationResult.Failure($"Contribution which Id was provided was not found");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.EntireCourse))
            {
                return OperationResult.Failure(
                    $"'{PaymentOptions.EntireCourse.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
            }

            var customerStripeAccountId = user.CustomerStripeAccountId;

            if (customerStripeAccountId is null)
            {
                _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                return OperationResult.Failure("Stripe customer is not attached to the user");
            }

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);

            var contributor = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(x => x.Id == purchase.ContributorId);


            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
            PurchasePayment payment = null;

            if (purchaseVm != null)
            {
                if (purchaseVm.HasProcessingPayment)
                {
                    return OperationResult.Failure("Contribution payment is in processing. Try later");
                }

                if (purchaseVm.HasUnconfirmedPayment)
                {
                    return OperationResult.Failure(
                        "You can't change payment method of your payment till it's unconfirmed. Cancel your payment instead");
                }

                if (purchaseVm.HasSucceededPayment)
                {
                    return OperationResult.Failure("You have already purchased this contribution");
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.SplitPayments && purchaseVm.SubscriptionId != null)
                {
                    var subscriptionResult =
                        await _stripeService.GetProductPlanSubscriptionAsync(purchaseVm.SubscriptionId);

                    if (!subscriptionResult.Succeeded)
                    {
                        return subscriptionResult;
                    }

                    var subscription = subscriptionResult.Payload;
                    if (subscription != null)
                    {
                        if (subscription.Status != "canceled")
                        {
                            var cancellationResult =
                                await _stripeService.CancelProductPlanSubscriptionScheduleAsync(
                                    subscription.Schedule.Id);

                            if (!cancellationResult.Succeeded)
                            {
                                return cancellationResult;
                            }
                        }

                        var latestInvoice = subscription.LatestInvoice;

                        if (latestInvoice != null && latestInvoice.Status != "draft" && latestInvoice.Status != "void")
                        {
                            var voidResult = await _stripeService.VoidInvoiceAsync(subscription.LatestInvoiceId);

                            if (!voidResult.Succeeded)
                            {
                                return voidResult;
                            }
                        }
                    }

                    purchaseVm.SubscriptionId = null;
                    purchase = _mapper.Map<Purchase>(purchaseVm);
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.EntireCourse)
                {
                    //Attempt to find incomplete Payment and then to REUSE related Stripe Payment Intent
                    payment = purchaseVm.Payments.FirstOrDefault(x =>
                        x.PaymentStatus == PaymentStatus.RequiresPaymentMethod
                        && x.PaymentOption == PaymentOptions.EntireCourse);
                }
            }

            //Calculates Contribution Cost in the smallest currency unit
            var purchaseAmountInCents = contribution.PaymentInfo.Cost.Value * _stripeService.SmallestCurrencyUnit;

            if (contribution.PaymentInfo.PackageSessionDiscountPercentage.HasValue)
            {
                var discount = Convert.ToDecimal(contribution.PaymentInfo.PackageSessionDiscountPercentage) / 100;
                purchaseAmountInCents -= purchaseAmountInCents * discount;
            }

            var contributionOwner =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);

            var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(contributionOwner.AccountId);

            var serviceProviderIncome = _pricingCalculationService.CalculateServiceProviderIncomeAsLong(
                purchaseAmountInCents,
                contribution.PaymentInfo.CoachPaysStripeFee,
                currentPaidTier.PaidTierOption.NormalizedFee,
                contribution.PaymentType, contributionOwner.CountryId);

            //Adds Processing Fee
            var purchaseGrossAmount = _calculationFeeService.CalculateGrossAmountAsLong(
                purchaseAmountInCents, contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId);

            PaymentIntent paymentIntent = null;
            int? paymentSessionLifetimeSeconds = null;

            var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == contributionOwner.AccountId);

            if (payment != null)
            {
                //Trying to REUSE existing Incomplete Stripe Payment Intent
                paymentIntent = await _stripeService.GetPaymentIntentAsync(payment.TransactionId);

                //Updating existing Payment Intent with new calculated contribution Amount
                PaymentIntentUpdateViewModel updateViewModel = new PaymentIntentUpdateViewModel
                {
                    Id = paymentIntent.Id,
                    Amount = purchaseGrossAmount
                };
                if (contributor.IsBetaUser)
                {
                    updateViewModel.TransferAmount = serviceProviderIncome;
                    updateViewModel.ConnectedAccountId = contributor.ConnectedStripeAccountId;
                }
                var paymentIntentResult = await _stripeService.UpdatePaymentIntentAsync(updateViewModel);

                if (!paymentIntentResult.Succeeded)
                {
                    return paymentIntentResult;
                }

                paymentIntent = paymentIntentResult.Payload;
            }
            else
            {
                var contributionOwnerUser = await _unitOfWork.GetRepositoryAsync<User>()
                    .GetOne(e => e.AccountId == cohealerAccount.Id);
                var Payment_Option = contribution.PaymentInfo.PaymentOptions.First().ToString();

                var model = GetCreatePaymentIntentModel(
                    user,
                    accountEmail,
                    cohealerAccount,
                    contributionId,
                    purchaseAmountInCents,
                    serviceProviderIncome,
                    purchaseGrossAmount,
                    contributionOwnerUser,
                    Payment_Option,
                    coupon);


                //Creating new Payment Intent if any incomplete existing was not found
                var paymentIntentResult = await _stripeService.CreatePaymentIntentAsync(model, contribution.DefaultCurrency, contributor.ConnectedStripeAccountId);

                if (!paymentIntentResult.Succeeded)
                {
                    return paymentIntentResult;
                }

                paymentIntent = paymentIntentResult.Payload;

                if (paymentIntent.Status != PaymentStatus.Succeeded.GetName())
                {
                    //Payment Intent cancellation job scheduling
                    _jobScheduler.ScheduleJob<IPaymentCancellationJob>(
                        TimeSpan.FromSeconds(_paymentSessionLifetimeSeconds), paymentIntent.Id);
                    paymentSessionLifetimeSeconds = _paymentSessionLifetimeSeconds;
                }

                //Means that there is the first request for current contribution purchasing
                //Creating new local payment entry
                payment = new PurchasePayment
                {
                    TransactionId = paymentIntent.Id,
                    DateTimeCharged = paymentIntent.Created
                };
            }

            payment.PaymentStatus = paymentIntent.Status.ToPaymentStatusEnum();
            payment.PaymentOption = PaymentOptions.EntireCourse;
            payment.TransferAmount = serviceProviderIncome / _stripeService.SmallestCurrencyUnit;
            payment.PurchaseAmount = purchaseAmountInCents / _stripeService.SmallestCurrencyUnit;
            payment.GrossPurchaseAmount = purchaseGrossAmount / _stripeService.SmallestCurrencyUnit;
            payment.IsInEscrow = !contribution.InvitationOnly;

            payment.PurchaseCurrency = contribution.DefaultCurrency;
            payment.Currency = contribution.DefaultCurrency;

            await SetAffiliateIncome(payment, purchaseAmountInCents, cohealerAccount, currentPaidTier.PaidTierOption.NormalizedFee);

            if (purchase == null)
            {
                //Means that there is the first authorized Client request for current contribution purchasing
                purchase = new Purchase
                {
                    ClientId = user.Id,
                    ContributorId = contributionOwner.Id,
                    ContributionId = contributionId,
                    Payments = new List<PurchasePayment> { payment },
                    ContributionType = contribution.Type,
                    SplitNumbers = contribution.PaymentInfo.SplitNumbers,
                    PaymentType = contribution.PaymentType.ToString(),
                    TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
                };
            }
            else if (purchase.Payments.All(x => x.TransactionId != payment.TransactionId))
            {
                purchase.Payments.Add(payment);
            }

            if (purchase.Id != null)
            {
                _synchronizePurchaseUpdateService.Sync(purchase);
            }
            else
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(purchase);
            }

            var receiptAmount = purchaseGrossAmount / _stripeService.SmallestCurrencyUnit;
            var paymentViewModel = new ContributionPaymentIntentDetailsViewModel
            {
                ClientSecret = paymentIntent.ClientSecret,
                Currency = contribution.DefaultCurrency,
                Price = receiptAmount,
                PlatformFee = receiptAmount - contribution.PaymentInfo.Cost,
                Status = paymentIntent.Status,
                SessionLifeTimeSeconds = paymentSessionLifetimeSeconds,
                Created = paymentIntent.Created,
            };

            return OperationResult.Success(null, paymentViewModel);
        }

        private async Task SetAffiliateIncome(
            PurchasePayment payment,
            decimal purchaseAmountInCents,
            Account cohealerAccount,
            decimal platformPercentageFee)
        {
            if (cohealerAccount.InvitedBy != null)
            {
                var availableAffiliateRevenue =
                    await _affiliateCommissionService.GetAffiliateIncomeInCents(
                        purchaseAmountInCents,
                        platformPercentageFee,
                        cohealerAccount.Id);
                if (availableAffiliateRevenue > 0m)
                {
                    payment.AffiliateRevenueTransfer = new AffiliateRevenueTransfer
                    {
                        Amount = availableAffiliateRevenue / _stripeService.SmallestCurrencyUnit,
                        IsInEscrow = true
                    };
                }
            }
        }

        private PaymentIntentCreateViewModel GetCreatePaymentIntentModel(User user, string accountEmail,
            Account cohealerAccount, string contributionId, decimal purchaseAmount, long serviceProviderIncome,
            long purchaseGrossAmount, User contributionOwner, string PaymentOption, Coupon coupon)
        {
            // DYNAMIC FEE 
            var contribution =  _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId).GetAwaiter().GetResult();
            var contributoruser =  _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.Id == (contributionOwner.Id ?? contribution.UserId)).GetAwaiter().GetResult();
            Country country = null;
            StripeCountryFee dynamicStripeFee = null;
            if (contributoruser?.CountryId != null) country = _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == contributoruser.CountryId).GetAwaiter().GetResult();
            if(country?.Alpha2Code != null) dynamicStripeFee =  _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Alpha2Code).GetAwaiter().GetResult();
            // END

            var model = BaseCreatePeymentIntentModel(user, accountEmail, cohealerAccount, purchaseAmount,
                serviceProviderIncome, purchaseGrossAmount, contributionOwner, coupon);
            
            // DYNAMIC FEE
            model.ServiceAgreementType = contributoruser.ServiceAgreementType;
            model.International = dynamicStripeFee?.International ?? 3.9M;
            model.Fee = dynamicStripeFee?.Fee ?? 2.9M;
            model.Fixed = dynamicStripeFee?.Fixed ?? 0.30M;
            model.TotalChargedCost = purchaseGrossAmount;
            model.CoachPaysFee = contribution.PaymentInfo.CoachPaysStripeFee;
            if (coupon != null)
            {
                model.CouponID = coupon.Id;
                model.CouponPercent = coupon.PercentOff;
            }
            // END

            model.Metadata.Add(Constants.Contribution.Payment.MetadataIdKey, contributionId);
            model.Metadata.Add("PaymentOption", PaymentOption);
            return model;
        }

        private PaymentIntentCreateViewModel GetCreatePaymentIntentModel(User user, string accountEmail,
            Account cohealerAccount, BookOneToOneTimeResultViewModel bookedTimes,
            decimal purchaseAmount, long serviceProviderIncome, long purchaseGrossAmount, User contributionOwner, Coupon coupon)
        {
            var model = BaseCreatePeymentIntentModel(user, accountEmail, cohealerAccount, purchaseAmount,
                serviceProviderIncome, purchaseGrossAmount, contributionOwner, coupon);

            model.Metadata.Add(
                Constants.Contribution.Payment.MetadataIdKey,
                bookedTimes.ContributionId);

            model.Metadata.Add(
                Constants.Contribution.Payment.AvailabilityTimeIdBookedTimeIdPairsKey,
                JsonConvert.SerializeObject(bookedTimes.AvailabilityTimeIdBookedTimeIdPairs));

            return model;
        }

        private PaymentIntentCreateViewModel BaseCreatePeymentIntentModel(User user, string accountEmail,
            Account cohealerAccount, decimal purchaseAmount, long serviceProviderIncome,
            long purchaseGrossAmount, User contributionOwner, Coupon coupon)
        {
            var connectedStripeAccountId = contributionOwner.ConnectedStripeAccountId;
            var customerStripeAccountId = user.CustomerStripeAccountId;

            var model = new PaymentIntentCreateViewModel()
            {
                CustomerId = customerStripeAccountId,
                Amount = purchaseGrossAmount,
                ReceiptEmail = accountEmail,
                TransferAmount = serviceProviderIncome,
                ConnectedAccountId = connectedStripeAccountId,
                PurchaseAmount = (cohealerAccount.InvitedBy != null) ? purchaseAmount : default
            };
            if (coupon != null)
            {
                model.CouponID = coupon.Id;
                model.CouponPercent = coupon.PercentOff;
            }

            model.Metadata.Add(
                Constants.Contribution.Payment.TransferMoneyDataKey,
                JsonConvert.SerializeObject(model));

            return model;
        }


        public async Task<OperationResult<string>> SubscribeToMembershipContributionAsync(
            string accountId,
            string contributionId,
            string couponId,
            PaymentOptions paymentOption,
            string accessCode)
        {
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);
            var contribution = await _contributionRootService.GetOne(contributionId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(clientUser, clientAccount, contribution);

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.Id == contribution.UserId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == coachUser.AccountId);

            if (contribution == null || contribution.Status != ContributionStatuses.Approved)
            {
                return OperationResult<string>.Failure("Contribution which Id was provided was not found");
            }

            if (contribution.Type != nameof(ContributionMembership))
            {
                return OperationResult<string>.Failure("Only membership contribution supported here");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOption) && paymentOption != PaymentOptions.Free)
            {
                return OperationResult<string>.Failure(
                    $"'{PaymentOptions.SplitPayments.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
            }

            if (contribution.PaymentType == PaymentTypes.Advance && (!coachUser.IsStandardAccount || string.IsNullOrEmpty(coachUser.StripeStandardAccountId)))
            {
                return OperationResult<string>.Failure("unsupported payment type for contribtuion", "unsupported payment type for contribtuion. Advance payment is enable for the Stripe standard account only");
            }
            var standardAccountId = string.Empty;
            if (coachUser.IsStandardAccount && contribution.PaymentType == PaymentTypes.Advance) standardAccountId = coachUser.StripeStandardAccountId;

            var customerStripeAccountId = clientUser.CustomerStripeAccountId;

            if (customerStripeAccountId is null)
            {
                _logger.LogError($"customer with accountId {clientUser.AccountId} has no Stripe Account");
                return OperationResult<string>.Failure("Stripe customer is not attached to the user");
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(x => x.ContributionId == contributionId && x.ClientId == clientUser.Id);

            var purchase = purchases.OrderByDescending(e => e.CreateTime).FirstOrDefault();

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (purchaseVm != null)
            {
                if (purchaseVm?.Subscription?.Status == "active")
                {
                    return OperationResult<string>.Failure("You've already purchased this contribution");
                }
            }

            // check if 100% coupon code applies here
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult<string>.Success(purchaseResult?.Payload as string);
                    }
                }
            }

            // check if session payment option is free
            if (paymentOption == PaymentOptions.Free)
            {
                var isAccessCodeValid = validateAccessCode(accessCode, contributionId);
                if (isAccessCodeValid)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult<string>.Success(purchaseResult?.Payload as string);
                    }
                }
            }

            var billingInfo = contribution.PaymentInfo.MembershipInfo.ProductBillingPlans[paymentOption];

            var model = new CreateCheckoutSessionModel
            {
                ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                ServiceAgreementType = coachUser.ServiceAgreementType,
                StripeCustomerId = customerStripeAccountId,
                PriceId = billingInfo.ProductBillingPlanId,
                BillingInfo = billingInfo,
                ContributionId = contribution.Id,
                PaymentOption = paymentOption,
                CouponId = couponId,
                IsStandardAccount = coachUser.IsStandardAccount,
                paymentType = contribution.PaymentType,
                StripeStandardAccountId = coachUser.StripeStandardAccountId,
                ClientEmail = clientAccount.Email,
                ClientFirstName = clientUser.FirstName,
                ClientLastName = clientUser.LastName,
                CoachEmail = coachAccount.Email,
                ContributionTitle = contribution.Title,
                TaxType = contribution.TaxType
            };

            var createCheckoutSessionResult = await _stripeService.CreateSubscriptionCheckoutSession(model);
            if (createCheckoutSessionResult.Succeeded)
            {
                if (contribution.PaymentType == PaymentTypes.Advance)
                {
                    return OperationResult<string>.Success(String.Empty, (string)createCheckoutSessionResult.Payload.RawJObject["url"]);
                }
                return OperationResult<string>.Success(String.Empty, createCheckoutSessionResult.Payload.Id);
            }
            else
            {
                return OperationResult<string>.Failure(createCheckoutSessionResult.Message);
            }
        }

        public async Task CreateNewStripeCustomerWithSameCurrency(User user, Account account, ContributionBase contribution)
        {
            string standardAccountId = string.Empty;
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            if (coachUser != null && coachUser.IsStandardAccount && contribution.PaymentType == PaymentTypes.Advance)
            {
                standardAccountId = coachUser.StripeStandardAccountId;
            }

            if (!string.IsNullOrWhiteSpace(contribution.DefaultCurrency))
            {
                var stripeCustomers = (List<StripeCustomerAccount>)_stripeAccountService.GetCustomerAccountList(account.Email, standardAccountId).Payload;
                if (!string.IsNullOrWhiteSpace(user.CustomerStripeAccountId) && string.IsNullOrWhiteSpace(stripeCustomers?.Where(a => a.CustomerId == user.CustomerStripeAccountId).FirstOrDefault()?.Currency))
                {
                    var customerResult = await GetOrCreateCustomer(user, account.Email, coachUser.StripeStandardAccountId, contribution.PaymentType, contribution.DefaultCurrency);
                    if (customerResult.Succeeded) //user collection already updated in the getOrCreate function
                    {
                        return;
                    }
                }

                var CustomerStripeAccountId = stripeCustomers.Where(a => a.Currency == contribution.DefaultCurrency).FirstOrDefault();
                if (CustomerStripeAccountId == null )
                {
                    var emptyCurencyAccount = stripeCustomers.Where(a => string.IsNullOrWhiteSpace(a.Currency)).FirstOrDefault();
                    if (emptyCurencyAccount != null)
                        user.CustomerStripeAccountId = emptyCurencyAccount.CustomerId;
                    else
                    {
                        var createCustomerAccountResult = await _stripeAccountService.CreateCustomerAsync(account.Email, createNew: true, standardAccountId);
                        user.CustomerStripeAccountId = createCustomerAccountResult.Payload;
                    }
                }
                else if (CustomerStripeAccountId.CustomerId == user.CustomerStripeAccountId)
                {
                    return;
                }
                else
                {
                    user.CustomerStripeAccountId = CustomerStripeAccountId.CustomerId;
                }
                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            }
        }

        public async Task<OperationResult> SubscribeToCourseContributionSplitPaymentsAsync(string accountId,
            string contributionId, string paymentMethodId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var contribution = await _contributionRootService.GetOne(contributionId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);

            //Check if the Client's currency doesn't match with contribution currency then create a new customer Stripe Account to enable client purchase in different currency
            await CreateNewStripeCustomerWithSameCurrency(user, account, contribution);

            var customerStripeAccountId = user.CustomerStripeAccountId;
            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.Id == contribution.UserId);

            if(contributorUser.IsBetaUser && !string.IsNullOrEmpty(contributorUser?.ServiceAgreementType) && contributorUser?.ServiceAgreementType == "full")
            {
                return OperationResult.Failure("Subscription/Split Payments is disabled for beta users");
            }
            var validationResult = Validate(contribution, customerStripeAccountId);

            if (validationResult.Failed)
            {
                return validationResult;
            }

            
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(x => x.ContributionId == contributionId && x.ClientId == user.Id);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            var standardAccountId = string.Empty;
            if (contributionAndStandardAccountIdDic.ContainsKey(contribution.Id)) standardAccountId = contributorUser.StripeStandardAccountId;

            Subscription subscription = null;
            int? paymentSessionLifetimeSeconds = null;

            if (purchaseVm != null)
            {
                if (purchaseVm.HasProcessingPayment)
                {
                    return OperationResult.Failure("Contribution payment is in processing. Try later");
                }

                if (purchaseVm.HasUnconfirmedPayment)
                {
                    return OperationResult.Failure(
                        "You can not use another payment method until there is unconfirmed payment. Cancel your payment instead");
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.EntireCourse)
                {
                    if (purchaseVm.HasSucceededPayment)
                    {
                        return OperationResult.Failure("You've already purchased this contribution");
                    }

                    //Attempt to find incomplete Payment and then to cancel related Stripe Payment Intent
                    var incompletePayment =
                        purchaseVm.Payments.FirstOrDefault(x => x.PaymentStatus == PaymentStatus.RequiresPaymentMethod);

                    if (incompletePayment != null)
                    {
                        var cancellationResult =
                            await _stripeService.CancelPaymentIntentAsync(incompletePayment.TransactionId);

                        if (cancellationResult.Failed)
                        {
                            return cancellationResult;
                        }
                    }
                }

                if (purchaseVm.RecentPaymentOption == PaymentOptions.SplitPayments)
                {
                    if (purchaseVm.Payments.All(x => x.PaymentStatus != PaymentStatus.RequiresPaymentMethod)
                        && purchaseVm.IsPaidAsSubscription)
                    {
                        return OperationResult.Failure("You have already purchased this contribution");
                    }

                    if (purchaseVm.SubscriptionId != null)
                    {
                        var subscriptionResult =
                            await _stripeService.GetProductPlanSubscriptionAsync(purchaseVm.SubscriptionId);

                        if (subscriptionResult.Failed)
                        {
                            return OperationResult.Failure("Subscription was not found");
                        }

                        subscription = subscriptionResult.Payload;

                        if (subscription.Status != "canceled")
                        {
                            var updatingResult = await _stripeService.UpdateProductPlanSubscriptionPaymentMethodAsync(
                                new UpdatePaymentMethodViewModel
                                { Id = subscription.Id, PaymentMethodId = paymentMethodId });

                            if (updatingResult.Failed)
                            {
                                return updatingResult;
                            }

                            subscription = updatingResult.Payload;
                        }

                        var paymentIntent = subscription.LatestInvoice.PaymentIntent;

                        if (paymentIntent.Status == PaymentStatus.RequiresPaymentMethod.GetName())
                        {
                            if (subscription.Status == "canceled" &&
                                !purchaseVm.Payments.Exists(x => x.PaymentStatus == PaymentStatus.Succeeded))
                            {
                                var latestInvoice = subscription.LatestInvoice;

                                if (latestInvoice != null && latestInvoice.Status != "draft" &&
                                    latestInvoice.Status != "void")
                                {
                                    var voidResult =
                                        await _stripeService.VoidInvoiceAsync(subscription.LatestInvoiceId);

                                    if (voidResult.Failed)
                                    {
                                        return voidResult;
                                    }
                                }
                            }
                            else
                            {
                                var intentUpdateResult = await _stripeService.UpdatePaymentIntentPaymentMethodAsync(
                                    new UpdatePaymentMethodViewModel
                                    { Id = paymentIntent.Id, PaymentMethodId = paymentMethodId });

                                if (intentUpdateResult.Failed)
                                {
                                    return intentUpdateResult;
                                }
                            }

                            subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(subscription.Id, null);

                            if (subscriptionResult.Failed)
                            {
                                return subscriptionResult;
                            }

                            subscription = subscriptionResult.Payload;
                            paymentIntent = subscription.LatestInvoice.PaymentIntent;
                        }

                        if (paymentIntent.Status == PaymentStatus.Canceled.GetName())
                        {
                            subscription = null;
                        }
                    }
                }

                purchase = _mapper.Map<Purchase>(purchaseVm);
            }

            if (subscription is null)
            {
                if (!contribution.PaymentInfo.SplitNumbers.HasValue)
                {
                    return OperationResult.Failure("Contribution split numbers are not specified");
                }

                var customerStripeId = user.CustomerStripeAccountId;

                var subscribeModel = new ProductSubscriptionViewModel
                {
                    CustomerId = customerStripeId,
                    StripeSubscriptionPlanId = contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId,
                    DefaultPaymentMethod = paymentMethodId,
                    Iterations = contribution.PaymentInfo.SplitNumbers.Value,

                    ConnectedStripeAccountId = contributorUser.ConnectedStripeAccountId,
                    ServiceAgreementType = contributorUser.ServiceAgreementType,
                    BillingInfo = contribution.PaymentInfo.BillingPlanInfo
                };

                var subscriptionResult =
                    await _stripeService.ScheduleSubscribeToProductPlanAsync(subscribeModel, contribution.Id, PaymentOptions.SplitPayments.ToString());

                if (subscriptionResult.Failed)
                {
                    return subscriptionResult;
                }

                subscription = subscriptionResult.Payload;

                var invoiceFinalizationResult = await _stripeService.FinalizeInvoiceAsync(subscription.LatestInvoiceId);

                if (invoiceFinalizationResult.Failed)
                {
                    return invoiceFinalizationResult;
                }

                subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(subscription.Id);

                if (subscriptionResult.Failed)
                {
                    return subscriptionResult;
                }

                subscription = subscriptionResult.Payload;

                if (subscription.LatestInvoice.PaymentIntent.Status != PaymentStatus.Succeeded.GetName())
                {
                    _jobScheduler.ScheduleJob<ISubscriptionCancellationJob>(
                        TimeSpan.FromSeconds(_paymentSessionLifetimeSeconds), subscription.Id, standardAccountId);
                    paymentSessionLifetimeSeconds = _paymentSessionLifetimeSeconds;
                }
            }

            var payment = purchase?.Payments.FirstOrDefault(x =>
                x.TransactionId == subscription.LatestInvoice.PaymentIntentId);

            if (payment is null)
            {
                payment = new PurchasePayment
                {
                    TransactionId = subscription.LatestInvoice.PaymentIntentId,
                    DateTimeCharged = subscription.LatestInvoice.PaymentIntent.Created
                };
            }

            payment.PaymentStatus = subscription.LatestInvoice.PaymentIntent.Status.ToPaymentStatusEnum();
            payment.PaymentOption = PaymentOptions.SplitPayments;
            payment.GrossPurchaseAmount =
                subscription.LatestInvoice.PaymentIntent.Amount / _stripeService.SmallestCurrencyUnit;
            payment.PurchaseAmount = contribution.PaymentInfo.BillingPlanInfo.BillingPlanPureCost;
            payment.IsInEscrow = !contribution.InvitationOnly;

            payment.PurchaseCurrency = contribution.DefaultCurrency;
            payment.Currency = contribution.DefaultCurrency;

            if (purchase is null)
            {
                purchase = new Purchase
                {
                    ClientId = user.Id,
                    ContributorId = contribution.UserId,
                    ContributionId = contributionId,
                    Payments = new List<PurchasePayment> { payment },
                    ContributionType = contribution.Type,
                    SplitNumbers = contribution.PaymentInfo.SplitNumbers,
                    PaymentType = contribution.PaymentType.ToString(),
                    TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
                };
            }
            else if (purchase.Payments.All(x => x.TransactionId != subscription.LatestInvoice.PaymentIntentId))
            {
                purchase.Payments.Add(payment);
            }

            purchase.SubscriptionId = subscription.Id;

            if (purchase.Id is null)
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(purchase);
            }
            else
            {
                _synchronizePurchaseUpdateService.Sync(purchase);
            }

            var paymentViewModel = new ContributionPaymentIntentDetailsViewModel()
            {
                ClientSecret = subscription.LatestInvoice.PaymentIntent.ClientSecret,
                Currency = contribution.DefaultCurrency,
                Price = subscription.LatestInvoice.PaymentIntent.Amount / _stripeService.SmallestCurrencyUnit,
                Status = subscription.LatestInvoice.PaymentIntent.Status,
                SessionLifeTimeSeconds = paymentSessionLifetimeSeconds,
                Created = subscription.Created,
            };

            return OperationResult.Success(null, paymentViewModel);

            OperationResult Validate(ContributionBase contribution, string customerStripeAccountId)
            {
                if (contribution is null || contribution.Status != ContributionStatuses.Approved)
                {
                    return OperationResult.Failure("Contribution which Id was provided was not found");
                }

                if (!contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments))
                {
                    return OperationResult.Failure(
                        $"'{PaymentOptions.SplitPayments.ToString()}' payment option is not allowed for '{contribution.Title}' contribution");
                }

                if (string.IsNullOrEmpty(customerStripeAccountId))
                {
                    _logger.LogError($"customer with accountId {user.AccountId} has no Stripe Account");
                    return OperationResult.Failure("Stripe customer is not attached to the user");
                }

                return OperationResult.Success();
            }
        }

        public OperationResult HandlePaymentIntentStripeEvent(StripeEvent @event, bool forStandardAccount, bool isPaidByInvoice = false, Invoice fullInvoice = null)
        {
            try
            {
                var paymentIntent = new PaymentIntent();
                if (isPaidByInvoice)
                {
                    paymentIntent = fullInvoice.PaymentIntent;
                }
                else if (@event.Data.Object is PaymentIntent)
                {
                    paymentIntent = (PaymentIntent)@event.Data.Object;
                }

                if (paymentIntent is not null)
                {
                    var accountId = string.Empty;
                    if (forStandardAccount) accountId = @event.Account;
                    if (!ResolveCurrentContribution(paymentIntent, out var contribution, accountId))
                    {
                        _logger.Log(LogLevel.Error, $"{Constants.Contribution.Payment.StripeWebhookErrors.ContributionNotFound} in ResolveCurrentContribution at {DateTime.UtcNow} for contribution {contribution.Id}  ");
                        return OperationResult.Failure(Constants.Contribution.Payment.StripeWebhookErrors
                            .ContributionNotFound);
                    }

                    var contributionOwner = _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId).GetAwaiter().GetResult();
                    string standardAccountId = string.Empty;
                    if (contribution.PaymentType == PaymentTypes.Advance && contributionOwner.IsStandardAccount)
                    {
                        standardAccountId = contributionOwner.StripeStandardAccountId;
                    }

                    BalanceTransaction balanceTransaction = null;
                    var isPaymentIntentSucceeded = @event.Type == Events.PaymentIntentSucceeded;
                    var isInvoicePaid = isPaidByInvoice && @event.Type == Events.InvoicePaid;       //payment can also be paid from the invoice or payement intent. Handle both event type
                    if ((isPaymentIntentSucceeded || isInvoicePaid) & paymentIntent.Charges.Any(x => x.Status == "succeeded"))
                    {
                        try
                        {
                            var lastCharge = paymentIntent.Charges.Data.Last(x => x.Status == "succeeded");
                            if (lastCharge.BalanceTransaction == null)
                            {
                                balanceTransaction = _balanceTransactionService.Get(lastCharge.BalanceTransactionId, options: null, _stripeService.GetStandardAccountRequestOption(standardAccountId));
                            }
                            else
                            {
                                balanceTransaction = lastCharge.BalanceTransaction;
                            }

                            //balanceTransaction.Amount = paymentIntent.Amount;
                            //balanceTransaction.Currency = paymentIntent.Currency;
                            //if (balanceTransaction.ExchangeRate > 0)
                            //{
                            //    balanceTransaction.Fee = (long)(balanceTransaction.Fee / balanceTransaction.ExchangeRate);
                            //    balanceTransaction.ExchangeRate = 0;
                            //}



                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "error during getting balance transaction");
                        }
                    }

                    var isSubscriptionIntent = paymentIntent.InvoiceId != null;
                    if (isSubscriptionIntent) paymentIntent.Invoice = _invoiceService.GetAsync(paymentIntent.InvoiceId, options: null, _stripeService.GetStandardAccountRequestOption(standardAccountId)).GetAwaiter().GetResult();

                    var client = _unitOfWork.GetRepositoryAsync<User>()
                        .GetOne(x => x.CustomerStripeAccountId == paymentIntent.CustomerId).GetAwaiter().GetResult();
                    var paymentOption = GetPaymentOptionAsync(paymentIntent, contribution, standardAccountId, isPaidByInvoice).GetAwaiter().GetResult();

                    _logger.LogInformation($"{@event.Type} is event type of contribution {contribution.Id}.");

                    var clientPurchase = this.AddOrUpdatePurchaseAsync(contribution, paymentOption, paymentIntent, balanceTransaction, client, contributionOwner, isPaidByInvoice, accountId).GetAwaiter().GetResult();
                    if (clientPurchase == null)
                    {
                        _logger.LogInformation($"{@event.Type} is event type of contribution {contribution.Id} for client {client.Id} in failed case.");
                        _logger.Log(LogLevel.Error, $"Purchase of contribution ID: '{contribution.Id}' " +
                                                        $"associated with the client ID: '{client.Id} was not found' at {DateTime.UtcNow}.");
                        return OperationResult.Failure($"Purchase of contribution ID: '{contribution.Id}' " +
                                                        $"associated with the client ID: '{client.Id} was not found'");
                    }

                    var payment = clientPurchase?.Payments?.FirstOrDefault(x => x.TransactionId == paymentIntent.Id);

                    if (payment is null)
                    {
                        _logger.Log(LogLevel.Error, $"Payment associated with Payment Intent ID: '{paymentIntent.Id}' was not found at {DateTime.UtcNow} for contribution {contribution.Id}.");
                        return OperationResult.Failure(
                            $"Payment associated with Payment Intent ID: '{paymentIntent.Id}' was not found");
                    }
                    
                    if (payment.PaymentStatus != PaymentStatus.Succeeded)
                    {
                        payment.PaymentStatus = paymentIntent.Status.ToPaymentStatusEnum();
                    }

                    if (contribution.PaymentType != PaymentTypes.Advance)
                    {
                        if (payment.PaymentStatus == PaymentStatus.Succeeded &&
                                        paymentIntent.Charges.Any(x => x.Status == "succeeded"))
                        {

                            var transferResult = CreateMoneyTransfer(paymentIntent, contribution, decimal.ToInt64(payment.TransferAmount * _stripeService.SmallestCurrencyUnit),
                                decimal.ToInt64(payment.PurchaseAmount * _stripeService.SmallestCurrencyUnit));
                            if (!transferResult.Succeeded)
                            {
                                _logger.LogInformation($"Transfer result not succeeded for contrinution {contribution.Id} & {client.Id}");
                                return transferResult;
                            }
                            HandleTransferResult(clientPurchase, payment, transferResult);

                            if (isSubscriptionIntent)
                            {
                                clientPurchase.DeclinedSubscriptionPurchase = null;
                            }

                            var transferedamount = transferResult.Payload;
                            //Geting transaaction info of received amount 
                            var contributorUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId)
                           .GetAwaiter().GetResult();
                            var blanceservice = new BalanceTransactionService();
                            var desitinationBalanceTransaction = blanceservice.List(null, new RequestOptions { StripeAccount = contributorUser.ConnectedStripeAccountId }).Where(x => x.SourceId == transferedamount.CoachTransfer.DestinationPaymentId).FirstOrDefault();
                            PurchasePayment paymentobj = clientPurchase.Payments.Where(x => x.TransactionId == paymentIntent.Id).FirstOrDefault();


                            if (!string.IsNullOrEmpty(contribution.DefaultCurrency) && contribution.DefaultCurrency != balanceTransaction.Currency)
                            {
                                paymentobj.TransferAmount = (desitinationBalanceTransaction.Amount - desitinationBalanceTransaction.Fee) / _stripeService.SmallestCurrencyUnit;
                                //paymentobj.PurchaseAmount = Math.Round((decimal)(paymentobj.PurchaseAmount / balanceTransaction.ExchangeRate ?? 1), 2);
                                paymentobj.Currency = contribution.DefaultCurrency;
                                //paymentobj.ProcessingFee = Math.Round((decimal)(paymentobj.ProcessingFee / balanceTransaction.ExchangeRate ?? 1), 2);
                                //paymentobj.ClientFee = Math.Round((decimal)(paymentobj.ClientFee / balanceTransaction.ExchangeRate ?? 1), 2);
                                //paymentobj.CohereFee = Math.Round((decimal)(paymentobj.CohereFee / balanceTransaction.ExchangeRate ?? 1), 2);
                                //paymentobj.CoachFee = Math.Round((decimal)(paymentobj.CoachFee / balanceTransaction.ExchangeRate ?? 1), 2);
                                paymentobj.GrossPurchaseAmount = paymentIntent.Charges.FirstOrDefault().Amount / _stripeService.SmallestCurrencyUnit;
                                //paymentobj.GrossPurchaseAmount = Math.Round((decimal)(paymentobj.GrossPurchaseAmount / balanceTransaction.ExchangeRate ?? 1), 2);
                            }
                            if (contribution.PaymentInfo.CoachPaysStripeFee == false) // means clinet is paying the fee
                            {
                                decimal feeCalculated = (paymentobj.PurchaseAmount - paymentobj.TransferAmount);
                                decimal feeDifference = paymentobj.ClientFee - feeCalculated;
                                if (feeDifference > 0)
                                {
                                    paymentobj.CoachFee = feeDifference;
                                    paymentobj.ClientFee = feeCalculated;
                                    paymentobj.TransferAmount -= feeDifference;
                                }
                            }
                            else // Coach is paying the fee
                            {
                                decimal feeCalculated = (paymentobj.PurchaseAmount - paymentobj.TransferAmount);
                                decimal feeDifference = paymentobj.CoachFee - feeCalculated;
                                if (feeDifference > 0)
                                {
                                    paymentobj.TransferAmount -= feeDifference;
                                }

                            }


                            paymentobj.DestinationBalanceTransaction = new DestinationBalanceTransaction()
                            {
                                Amount = desitinationBalanceTransaction.Amount / _stripeService.SmallestCurrencyUnit,
                                Fee = desitinationBalanceTransaction.Fee / _stripeService.SmallestCurrencyUnit,
                                Currency = desitinationBalanceTransaction.Currency,
                                ExchangeRate = desitinationBalanceTransaction.ExchangeRate ?? balanceTransaction.ExchangeRate,
                                Net = desitinationBalanceTransaction.Net / _stripeService.SmallestCurrencyUnit,
                                SourceId = desitinationBalanceTransaction.SourceId
                            };
                            _unitOfWork.GetRepositoryAsync<Purchase>().Update(clientPurchase.Id, clientPurchase);


                            // active campaign
                            HandleAfterPurchaseActiveCampaignEvent(contribution, clientPurchase, contributionOwner);
                        } 
                    }
                    else
                    {
                        //bypass money transfer 
                        if (payment.PaymentStatus == PaymentStatus.Succeeded &&
                                        paymentIntent.Charges.Any(x => x.Status == "succeeded"))
                        {
                            PurchasePayment paymentobj = clientPurchase.Payments.Where(x => x.TransactionId == paymentIntent.Id).FirstOrDefault();

                            if (!string.IsNullOrEmpty(contribution.DefaultCurrency) && contribution.DefaultCurrency != balanceTransaction.Currency)
                            {
                                paymentobj.TransferAmount = (balanceTransaction.Amount - balanceTransaction.Fee) / _stripeService.SmallestCurrencyUnit;
                                paymentobj.GrossPurchaseAmount = paymentIntent.Charges.FirstOrDefault().Amount / _stripeService.SmallestCurrencyUnit;
                            }
                            if (contribution.PaymentInfo.CoachPaysStripeFee == false) // means clinet is paying the fee
                            {
                                decimal feeCalculated = (paymentobj.PurchaseAmount - paymentobj.TransferAmount);
                                decimal feeDifference = paymentobj.ClientFee - feeCalculated;
                                if (feeDifference > 0)
                                {
                                    paymentobj.CoachFee = feeDifference;
                                    paymentobj.ClientFee = feeCalculated;
                                    paymentobj.TransferAmount -= feeDifference;
                                }
                            }
                            else // Coach is paying the fee
                            {
                                decimal feeCalculated = (paymentobj.PurchaseAmount - paymentobj.TransferAmount);
                                decimal feeDifference = paymentobj.CoachFee - feeCalculated;
                                if (feeDifference > 0)
                                {
                                    paymentobj.TransferAmount -= feeDifference;
                                }
                            }

                            paymentobj.DestinationBalanceTransaction = new DestinationBalanceTransaction()
                            {
                                Amount = balanceTransaction.Amount / _stripeService.SmallestCurrencyUnit,
                                Fee = balanceTransaction.Fee / _stripeService.SmallestCurrencyUnit,
                                Currency = balanceTransaction.Currency,
                                ExchangeRate = balanceTransaction.ExchangeRate,
                                Net = balanceTransaction.Net / _stripeService.SmallestCurrencyUnit
                            };
                            _unitOfWork.GetRepositoryAsync<Purchase>().Update(clientPurchase.Id, clientPurchase);
                        }
                    }

                    if (contribution is ContributionOneToOne oneToOneContribution)
                    {
                        if (payment.PaymentOption == PaymentOptions.PerSession)
                        {
                            oneToOneContribution = contribution as ContributionOneToOne;
                            var oneToOneBookingResult = HandleOneToOneBooking(paymentIntent, contribution, client);
                            if (!oneToOneBookingResult.Succeeded)
                            {
                                return oneToOneBookingResult;
                            }
                            else
                            {
                                // update payment intent to include AvailabilityTimeIdBookedTimeIdPairsKey
                                paymentIntent = oneToOneBookingResult.Payload;
                            }
                        }

                        // get the updated contribution
                        contribution = _contributionRootService.GetOne(contribution.Id).GetAwaiter().GetResult();
                        oneToOneContribution = contribution as ContributionOneToOne;
                        var oneToOneHandleResult =
                             HandleOneToOneContributionPurchaseEvent(oneToOneContribution, paymentIntent, payment, client);

                        if (!oneToOneHandleResult.Succeeded)
                        {
                            return oneToOneHandleResult;
                        }
                    }

                    if (contribution is ContributionCourse courseContribution)
                    {
                        HandleCourseContributionPurchaseEvent(courseContribution, clientPurchase, payment, client);
                    }

                    if (payment.PaymentStatus == PaymentStatus.Succeeded)
                    {
                        if (!clientPurchase.IsFirstPaymentHandeled)
                        {
                            AfterFirstPaymentHandled(contribution, null, paymentIntent, clientPurchase, payment, balanceTransaction, client);
                           
                        }
                        clientPurchase.IsFirstPaymentHandeled = true;
                    }

                    // todo: since couponId was added to the purchasePayment level, consider removing it from the purchase level and refactor sales history accordingly
                    if (string.IsNullOrEmpty(clientPurchase.CouponId) && paymentIntent.Metadata.TryGetValue(
                                    Constants.Stripe.MetadataKeys.CouponId,
                                    out var couponId))
                    {
                        clientPurchase.CouponId = couponId;
                    }
                    _synchronizePurchaseUpdateService.Sync(clientPurchase);

                    try
                    {
                        _logger.LogInformation($"Before runing Auto-booking these are the parameters. ContributionId: {contribution.Id}, ClientPurchaseId: {clientPurchase.Id}, TransactionId: {payment.TransactionId} and ClientId: {client.Id}.");
                        //BookIfSingleSessionAsync(contribution.Id, clientPurchase.Id, payment.TransactionId, client.AccountId, false);
                        AfterSave(contribution, clientPurchase, payment, client);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
                    }

                    if (isPaidByInvoice)
                    {
                        //send invoice paid email to coach for the contrbution
                        _notificationService.SendInvoicePaidEmailToCoach(contributionOwner.AccountId, fullInvoice.CustomerEmail, fullInvoice.Number, contribution.Title);
                    }
                    return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{ex.Message} in HandlePaymentIntentStripeEvent at {DateTime.UtcNow}.");
            }
            _logger.Log(LogLevel.Error, $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(PaymentIntent).FullName}' type at {DateTime.UtcNow}.");
            return OperationResult.Failure(
                $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(PaymentIntent).FullName}' type");

            void HandleTransferResult(
                Purchase clientPurchase,
                PurchasePayment payment,
                OperationResult<CreateMoneyTransferResult> transferResult)
            {
                try
                {
                    var transfer = transferResult.Payload;
                    payment.TransferAmount = transfer.CoachTransfer.Amount / _stripeService.SmallestCurrencyUnit;

                    if (transfer.AffiliateTransfer != null)
                    {
                        var paymentsWithAffiliateRevenue =
                            clientPurchase.Payments.Where(e => e.AffiliateRevenueTransfer != null).ToList();

                        payment.AffiliateRevenueTransfer = new AffiliateRevenueTransfer()
                        {
                            Amount = transfer.AffiliateTransfer.Amount / _stripeService.SmallestCurrencyUnit,
                            IsInEscrow =
                                (paymentsWithAffiliateRevenue.All(e => e.IsInEscrow) &&
                                 paymentsWithAffiliateRevenue.Any()) || !paymentsWithAffiliateRevenue.Any()
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $" {ex.Message} in HandleTransferResult at {DateTime.UtcNow}.");
                }
            }
        }
        private  void BookIfSingleSessionAsync(string contributionId, string clientPurchaseId, string transactionId, string userAccountId, bool autobookingforFreeContrib)
        {
                var logId = new Random().Next();
                _logger.LogInformation($"AutoBooking Calling for {contributionId} and {userAccountId} @ logId:{logId}");
                try
                {
                    var contribution = _contributionRootService.GetOne(contributionId).GetAwaiter().GetResult();
                    if (!(contribution is SessionBasedContribution course))
                    {
                        _logger.LogError($"Only {nameof(ContributionCourse)} and {nameof(ContributionMembership)} and {nameof(ContributionCommunity)} supported for {nameof(BookIfSingleSessionTimeJob)} : clientId: {userAccountId} @ logId:{logId}");
                        return;
                    }
                    bool isAutobookingEnabled = false;
                    if (autobookingforFreeContrib)
                    {
                        isAutobookingEnabled = true;
                    }
                    else
                    {
                        var clientPurchase =  _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(e => e.Id == clientPurchaseId).GetAwaiter().GetResult();
                        var payment = clientPurchase.Payments.First(e => e.TransactionId == transactionId);
                        isAutobookingEnabled = (payment.PaymentStatus == PaymentStatus.Succeeded || payment.PurchaseAmount == 0 && payment.PaymentStatus == PaymentStatus.Paid)
                        && clientPurchase.Payments.Count(p => (p.PaymentStatus == PaymentStatus.Succeeded || p.PurchaseAmount == 0 && p.PaymentStatus == PaymentStatus.Paid)) == 1;
                        if (isAutobookingEnabled == false)
                        {
                            isAutobookingEnabled = payment.IsTrial == true && payment.PaymentStatus == PaymentStatus.Paid;
                        }
                    }
                    if (isAutobookingEnabled)
                    {
                        var sessionTimesToBook = course.Sessions.Where(session => !session.IsPrerecorded && session.SessionTimes.Count == 1);
                        var bookSessionTimeModels = new List<BookSessionTimeViewModel>();
                        foreach (var session in sessionTimesToBook)
                        {
                            if (!session.SessionTimes.FirstOrDefault().IsCompleted)
                            {
                                var sessionTime = session.SessionTimes.First();
                                var model = new BookSessionTimeViewModel()
                                {
                                    ContributionId = contribution.Id,
                                    SessionId = session.Id,
                                    SessionTimeId = sessionTime.Id,
                                };
                                bookSessionTimeModels.Add(model);
                            }
                            else _logger.LogInformation($"not completed session for clientId : {userAccountId} @ logId:{logId}");
                        }
                        try
                        {
                            if (bookSessionTimeModels.Count > 0)
                            {
                                _contributionBookingService.BookSessionTimeAsync(bookSessionTimeModels, userAccountId, logId);
                            }
                            else _logger.LogInformation($"bookSessionModel count is zero for clientId : {userAccountId} @ logId:{logId}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"error during booking single session time in {contributionId} for client {userAccountId} @ logId:{logId}");
                        }
                    }
                    else _logger.LogInformation($"Autobooking Not enable for clientId : {userAccountId} @ logId:{logId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"error during booking single session time in BookIfSingleSessionAsync with contributionId: {contributionId}  and ClientId: {userAccountId}");
                }
        
        }
        private async Task<Purchase> AddOrUpdatePurchaseAsync(ContributionBase contribution, PaymentOptions? paymentOptions,
            PaymentIntent paymentIntent, BalanceTransaction balanceTransaction,
            User client, User contributionOwner, bool isPaidByInvoice, string standadrdAccountId = null)
        {
            try
            {

                if (contribution == null || client == null || paymentIntent == null)
                {
                    return null;
                }

                var clientPurchase = _unitOfWork.GetRepositoryAsync<Purchase>()
                        .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == client.Id).GetAwaiter()
                        .GetResult();

                // first check if purcasePayment already exist
                if (clientPurchase?.Payments?.Exists(p => p.TransactionId == paymentIntent?.Id) == true)
                {
                    // check if we need to update transfer ammount
                    try
                    {
                        if (paymentIntent.Status.ToPaymentStatusEnum() == PaymentStatus.Succeeded &&
                                   paymentOptions == PaymentOptions.MonthlySessionSubscription)
                        {
                            var purchasePayment = clientPurchase?.Payments?.LastOrDefault(p => p.TransactionId == paymentIntent?.Id);
                            if (purchasePayment != null)
                            {
                                bool coachPaysStripeFeeForMonthlySession = contribution.PaymentInfo.CoachPaysStripeFee;
                                var contributionOwnerForMonthlySession = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
                                var currentPaidTierForMonthlySession = await _paidTiersService.GetCurrentPaidTier(contributionOwnerForMonthlySession.AccountId);
                                var serviceProviderIncomeForMonthlySession = _pricingCalculationService.CalculateServiceProviderIncome(
                                    balanceTransaction.Amount,
                                    coachPaysStripeFeeForMonthlySession,
                                    currentPaidTierForMonthlySession.PaidTierOption.NormalizedFee,
                                    contribution.PaymentType,
                                    contributionOwner.CountryId,
                                    balanceTransaction.Fee);
                                purchasePayment.TransferAmount = decimal.ToInt64(serviceProviderIncomeForMonthlySession.Total) / _stripeService.SmallestCurrencyUnit;
                                clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>().Update(clientPurchase.Id, clientPurchase);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        _logger.LogError(ex, $"Payment Purchase updation of contribution {contribution.Id} by the client {client.Id} failed in AddOrUpdatePurchaseAsync");
                    }
                    return clientPurchase;
                }

                if (paymentOptions == null || balanceTransaction == null)
                {   
                    return null;
                }

                if (clientPurchase == null)
                {
                    clientPurchase = new Purchase
                    {
                        ClientId = client.Id,
                        ContributorId = contribution.UserId,
                        ContributionId = contribution.Id,
                        ContributionType = contribution.Type,
                        PaymentType = contribution.PaymentType.ToString(),
                        TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty,
                        SubscriptionId = paymentIntent.Invoice?.SubscriptionId,
                        IsPaidByInvoice = isPaidByInvoice
                    };
                }

                var payments = clientPurchase?.Payments?.Count() > 0 ?
                    clientPurchase.Payments :
                    new List<PurchasePayment>();

                var paymentToAdd = new PurchasePayment
                {
                    TransactionId = paymentIntent.Id,
                    DateTimeCharged = paymentIntent.Created
                };

                bool coachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee;
                var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(contributionOwner.AccountId);
                var serviceProviderIncome = _pricingCalculationService.CalculateServiceProviderIncome(
                    balanceTransaction.Amount,
                    coachPaysStripeFee,
                    currentPaidTier.PaidTierOption.NormalizedFee,
                    contribution.PaymentType,
                    contributionOwner.CountryId,
                    balanceTransaction.Fee);

                paymentToAdd.PaymentStatus = paymentIntent.Status.ToPaymentStatusEnum();
                paymentToAdd.PaymentOption = (PaymentOptions)paymentOptions;
                paymentToAdd.TransferAmount = decimal.ToInt64(serviceProviderIncome.Total) / _stripeService.SmallestCurrencyUnit;
                // todo: complete this logic
                //paymentToAdd.TransferCurrency = 
                paymentToAdd.PurchaseAmount = balanceTransaction.Amount / _stripeService.SmallestCurrencyUnit;
                paymentToAdd.Currency = balanceTransaction.Currency;
                paymentToAdd.ProcessingFee = balanceTransaction.Fee / _stripeService.SmallestCurrencyUnit;
                paymentToAdd.CohereFee = decimal.ToInt64(serviceProviderIncome.PlatformFee) / _stripeService.SmallestCurrencyUnit;
                paymentToAdd.CoachFee = paymentToAdd.PurchaseAmount - paymentToAdd.TransferAmount - paymentToAdd.CohereFee;
                if (!coachPaysStripeFee)
                {
                    paymentToAdd.CoachFee -= paymentToAdd.ProcessingFee;
                }
                if (contributionOwner.IsBetaUser && coachPaysStripeFee) // for full service
                    paymentToAdd.CoachFee = paymentToAdd.ProcessingFee;

                paymentToAdd.ClientFee = coachPaysStripeFee ? 0 :
                    paymentToAdd.PurchaseAmount - paymentToAdd.TransferAmount - paymentToAdd.CoachFee - paymentToAdd.CohereFee;
                if (!coachPaysStripeFee)
                    paymentToAdd.GrossPurchaseAmount = paymentToAdd.PurchaseAmount + paymentToAdd.CoachFee;
                else
                    paymentToAdd.GrossPurchaseAmount = paymentToAdd.PurchaseAmount;

                paymentToAdd.PurchaseCurrency = contribution.DefaultCurrency;

                paymentToAdd.PurchaseCurrency = contribution.DefaultCurrency;
                paymentToAdd.ExchangeRate = balanceTransaction.ExchangeRate != null ? (decimal)balanceTransaction.ExchangeRate : 0;// exchange rate on which platform recieves the amount

                paymentToAdd.PurchaseCurrency = contribution.DefaultCurrency;
                paymentToAdd.ExchangeRate = balanceTransaction.ExchangeRate != null ? (decimal)balanceTransaction.ExchangeRate : 0;// exchange rate on which platform recieves the amount

                if (paymentIntent.Metadata.TryGetValue(
                                    Constants.Stripe.MetadataKeys.CouponId,
                                    out var couponId))
                {
                    paymentToAdd.CouponId = couponId;
                }
                paymentToAdd.IsInEscrow = !contribution.InvitationOnly;
                var clientPurchaseVm = _mapper.Map<PurchaseViewModel>(clientPurchase);
                switch (clientPurchase.ContributionType)
                {
                    case nameof(ContributionCourse):
                        var isPaidAsEntireCourse = clientPurchaseVm.IsPaidAsEntireCourse;
                        paymentToAdd.TotalCost = _cohealerIncomeService.CalculateTotalCostForContibutionCourse(isPaidAsEntireCourse,
                        contribution as ContributionCourse);
                        break;

                    case nameof(ContributionOneToOne):
                        var isPaidAsSessionPackage = clientPurchaseVm.IsPaidAsSessionPackage || paymentOptions == PaymentOptions.SessionsPackage; ;
                        paymentToAdd.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionOneToOne(isPaidAsSessionPackage,
                        contribution as ContributionOneToOne);
                        break;

                    case nameof(ContributionMembership):
                        paymentToAdd.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionMembership(paymentToAdd,
                        contribution as ContributionMembership);
                        break;

                    case nameof(ContributionCommunity):
                        paymentToAdd.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionCommunity(paymentToAdd,
                        contribution as ContributionCommunity);
                        break;

                    default:
                        throw new Exception("Unsupported contribution type");
                }
                payments.Add(paymentToAdd);

                clientPurchase.Payments = payments;

                if (paymentOptions == PaymentOptions.SplitPayments && contribution?.PaymentInfo?.SplitNumbers > 0)
                {
                    clientPurchase.SplitNumbers = contribution.PaymentInfo.SplitNumbers;
                }
                var isSubscriptionIntent = paymentIntent.InvoiceId != null;
                if (isSubscriptionIntent && (contribution is ContributionMembership || contribution is ContributionCommunity))
                {
                    var invoice = await _stripeService.GetInvoiceAsync(paymentIntent.InvoiceId, standadrdAccountId);
                    if (!string.IsNullOrEmpty(invoice?.SubscriptionId))
                    {
                        clientPurchase.SubscriptionId = invoice.SubscriptionId;
                    }
                }

                //add package informtion in contribution
                if (clientPurchaseVm.IsPaidAsSessionPackage || paymentOptions == PaymentOptions.SessionsPackage)
                {
                    var contributionOneToOne = _mapper.Map<ContributionOneToOne>(contribution);
                    contributionOneToOne.PackagePurchases.Add(new PackagePurchase
                    {
                        TransactionId = paymentIntent.Id,
                        UserId = client.Id,
                        SessionNumbers = contributionOneToOne.PaymentInfo.PackageSessionNumbers.Value,
                        IsConfirmed = false
                    });
                    await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);
                }

                Purchase purchaseToReturn = null;
                if (string.IsNullOrEmpty(clientPurchase.Id))
                {
                    purchaseToReturn = await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
                }
                else
                {
                    purchaseToReturn = await _unitOfWork.GetRepositoryAsync<Purchase>().Update(clientPurchase.Id, clientPurchase);
                }
                //Add follower in Profile Page
                try
                {
                    var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
                    await _profilePageService.AddFollowerToProfile(client.AccountId, contributor.AccountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unable to add folower in profile page : {ex.Message} for client ", client.AccountId, DateTime.Now.ToString("F"));
                }
                return purchaseToReturn;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, $"{contribution.Id} by the client {client.Id} failed in AddOrUpdatePurchaseAsync");
                return null;
            }

        }

        private async Task<PaymentOptions?> GetPaymentOptionAsync(PaymentIntent paymentIntent, ContributionBase contribution, string stripeAccountId, bool IsPaidAsInvoice)
        {
            try
            {
                PaymentOptions? paymentOption = null;

                if (IsPaidAsInvoice)
                {
                    if (paymentIntent.Metadata.TryGetValue(
                        Constants.Stripe.MetadataKeys.PaymentOption,
                        out var stringPaymentOption))
                    {
                        if (Enum.TryParse(stringPaymentOption, out PaymentOptions paymentOptionResult))
                        {
                            paymentOption = paymentOptionResult;
                        }
                        return paymentOption;
                    }
                }

                bool isSubscriptionIntent = paymentIntent.InvoiceId != null;
                if (isSubscriptionIntent)
                {
                    var invoice = paymentIntent.Invoice;
                    if (invoice == null)
                    {
                        invoice = await _invoiceService.GetAsync(paymentIntent.InvoiceId, options: null, _stripeService.GetStandardAccountRequestOption(stripeAccountId));
                    }
                    if (!string.IsNullOrWhiteSpace(invoice?.SubscriptionId))
                    {
                        var subscription = invoice?.Subscription;
                        if (subscription == null)
                        {
                            var subscriptionServie = new SubscriptionService();
                            subscription = await subscriptionServie.GetAsync(invoice.SubscriptionId, options: null, _stripeService.GetStandardAccountRequestOption(stripeAccountId));
                        }
                        if (subscription != null)
                        {
                            if (subscription.Metadata.TryGetValue(
                            Constants.Stripe.MetadataKeys.PaymentOption,
                            out var stringPaymentOption))
                            {
                                if (Enum.TryParse(stringPaymentOption, out PaymentOptions paymentOptionResult))
                                {
                                    paymentOption = paymentOptionResult;
                                }
                            }
                        }
                    }
                    if (paymentOption == null)
                    {
                        if (contribution?.PaymentInfo?.MembershipInfo?.PaymentOptionsMap != null)
                        {
                            var paymentOptionByProductPlan =
                                contribution.PaymentInfo.MembershipInfo.PaymentOptionsMap;

                            if (paymentOptionByProductPlan.TryGetValue(
                                invoice?.Subscription?.Plan?.Id,
                                out var paymentOptionResult))
                            {
                                paymentOption = paymentOptionResult;
                            }
                        }
                        else if (contribution?.PaymentInfo?.BillingPlanInfo != null)
                        {
                            if (contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId == invoice?.Subscription?.Plan?.Id)
                            {
                                paymentOption = PaymentOptions.SplitPayments;
                            }
                        }
                    }
                }
                else
                {
                    if (paymentIntent.Metadata.TryGetValue(
                        Constants.Stripe.MetadataKeys.PaymentOption,
                        out var stringPaymentOption))
                    {
                        if (Enum.TryParse(stringPaymentOption, out PaymentOptions paymentOptionResult))
                        {
                            paymentOption = paymentOptionResult;
                        }
                    }
                }
                return paymentOption;
            }
            catch (Exception ex)
            {

                _logger.LogError("Exception In GetPaymentOptionAsync", ex);
                throw ex;
             }
            
        }

        private void HandleAfterPurchaseActiveCampaignEvent(ContributionBase contribution, Purchase clientPurchase, User contributionOwner)
        {
            try
            {
                var allContributionPurchases = _unitOfWork.GetRepositoryAsync<Purchase>()
                    .Get(x => x.ContributorId == contribution.UserId).GetAwaiter()
                    .GetResult()?.ToList() ?? new List<Purchase>();
                allContributionPurchases.Add(clientPurchase);
                List<DateTime> purchasesDates = allContributionPurchases?.SelectMany(c => c.Payments)?
                    .Where(p => p.PaymentStatus == PaymentStatus.Succeeded || p.PaymentStatus == PaymentStatus.Paid)?
                    .Select(p => new DateTime(p.DateTimeCharged.Year, p.DateTimeCharged.Month, 1))?
                    .Distinct()?
                    .ToList() ?? new List<DateTime>();
                string acHasAchieved2Months = EnumHelper<HasAchieved2MonthsOfRevenue>.GetDisplayValue(purchasesDates?.Count > 1 ? HasAchieved2MonthsOfRevenue.Yes : HasAchieved2MonthsOfRevenue.No);
                int consecutiveMonths = 0;
                if (purchasesDates?.Count > 2)
                {
                    for (int i = 1; i < purchasesDates.Count; i++)
                    {
                        if (purchasesDates[i].AddMonths(-1) == purchasesDates[i - 1])
                        {
                            consecutiveMonths++;
                            if (consecutiveMonths == 3)
                            {
                                break;
                            }
                        }
                        else
                        {
                            consecutiveMonths = 0;
                        }

                    }
                }
                string acHasAchieved3ConsecutiveMonths = EnumHelper<HasAchieved3ConsecutiveMonthsOfRevenue>.GetDisplayValue(consecutiveMonths >= 3 ? HasAchieved3ConsecutiveMonthsOfRevenue.Yes : HasAchieved3ConsecutiveMonthsOfRevenue.No);

                string acRevenue = EnumHelper<Revenue>.GetDisplayValue(purchasesDates?.Count > 1 ? Revenue.MonthlyRevenue : Revenue.Revenue);

                ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
                ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                {
                    CohereAccountId = contributionOwner.AccountId,
                    Revenue = acRevenue,
                    HasAchieved2MonthsOfRevenue = acHasAchieved2Months,
                    HasAchieved3ConsecutiveMonthsOfRevenue = acHasAchieved3ConsecutiveMonths
                };
                _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
            }
            catch
            {

            }
        }

        public void AfterFirstPaymentHandled(ContributionBase contribution, Invoice fullInvoice, PaymentIntent paymentIntent, Purchase clientPurchase, PurchasePayment payment,
            BalanceTransaction balanceTransaction, User user)
        {
            try
            {
                _chatService.AddClientToContributionRelatedChat(clientPurchase.ClientId, contribution)
                    .GetAwaiter().GetResult();

                var isTrial = fullInvoice?.Subscription?.TrialEnd > DateTime.UtcNow;
                SucceededPaymentEmailViewModel amountModel = null; 
                if (isTrial && paymentIntent == null)
                {
                    try
                    {
                        AfterSave(contribution, clientPurchase, payment, user);
                        //BookIfSingleSessionAsync(contribution.Id, clientPurchase.Id, payment.TransactionId, user.AccountId, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
                    }
                }
                else
                {
                    amountModel = CalculateTotalPaymentAmount(contribution, fullInvoice, paymentIntent, payment, balanceTransaction);
                    _notificationService
                    .SendPaymentSuccessNotification(clientPurchase, contribution, amountModel).GetAwaiter()
                    .GetResult();
                }

                string paidAmount = null;
                Account clientAccount = null;
                if (fullInvoice != null) paidAmount = (fullInvoice?.Charge?.BalanceTransaction?.Amount ?? 0 / _stripeService.SmallestCurrencyUnit).ToString();
                //As user is being passed null so there is Exception (FIXED)
                if (user != null) 
                    clientAccount = _unitOfWork.GetRepositoryAsync<Account>()
                    .GetOne(e => e.Id == user.AccountId).GetAwaiter().GetResult();

                if(paidAmount != null && fullInvoice != null && paidAmount != "0" && clientAccount?.Email != null) 
                    _notificationService
                    .SendClientEnrolledNotification(contribution, user.FirstName, clientAccount.Email, paidAmount)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during adding to chats or during notifying user");
                // Ignore side errors and proceed
            }
        }

        private void AfterSave(
            ContributionBase contribution,
            Purchase clientPurchase,
            PurchasePayment payment,
            User user)
        {
            _logger.LogInformation($"ContributionId : {contribution.Id} & UserId : {user.Id}  executing AfterSave method.");
            _jobScheduler.EnqueueAdync<IBookIfSingleSessionTimeJob>(
                contribution.Id,
                clientPurchase.Id,
                payment.TransactionId,
                user.AccountId,
                false);
        }

        public OperationResult HandleCheckoutSessionCompletedEvent(Event @event, bool forStandardAccount)
        {
            try
            {
                if (@event.Data.Object is Stripe.Checkout.Session checkoutSession)
                {
                    var accountId = string.Empty;
                    if (forStandardAccount) accountId = @event.Account;

                    if (checkoutSession.SetupIntentId is null)
                    {
                        var paymentIntent = _stripeService.GetPaymentIntentAsync(checkoutSession.PaymentIntentId, accountId).GetAwaiter().GetResult();
                        if (paymentIntent != null && paymentIntent.Metadata != null)
                        {
                            paymentIntent.Metadata.TryGetValue(Constants.Contribution.Payment.MetadataIdKey, out var contributionId);

                            var contribution = _contributionRootService.GetOne(contributionId).GetAwaiter().GetResult();
                            var client = _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.CustomerStripeAccountId == paymentIntent.CustomerId).GetAwaiter().GetResult();
                            var clientAccount = _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == client.AccountId).GetAwaiter().GetResult();
                            var rawObject = @event.Data.RawObject;

                            if (int.TryParse(rawObject?.amount_total?.ToString(), out int amountSubtotal))
                            {
                                var paidAmount = (amountSubtotal / _stripeService.SmallestCurrencyUnit).ToString();

                                _notificationService
                                .SendClientEnrolledNotification(contribution, client.FirstName, clientAccount.Email, paidAmount)
                                .GetAwaiter()
                                .GetResult();
                            }
                        }
                        else
                        {
                            _logger.Log(LogLevel.Error,
                            @$"Payment Intent is empty for PaymentIntentId: {checkoutSession.PaymentIntentId}");
                        }
                        return OperationResult.Success();
                    }
                    
                    var setupIntent = _setupIntentService.Get(checkoutSession.SetupIntentId, options: null, _stripeService.GetStandardAccountRequestOption(accountId));
                    var options = new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = setupIntent.PaymentMethodId,
                        },
                    };
                    _customerService.Update(setupIntent.CustomerId, options, _stripeService.GetStandardAccountRequestOption(accountId));

                    try
                    {
                        var user = _unitOfWork.GetRepositoryAsync<User>()
                            .GetOne(e => e.CustomerStripeAccountId == setupIntent.CustomerId).GetAwaiter().GetResult();

                        var allDeclinedPurchases = _unitOfWork.GetRepositoryAsync<Purchase>()
                            .Get(e => e.ClientId == user.Id && e.DeclinedSubscriptionPurchase != null).GetAwaiter()
                            .GetResult();

                        foreach (var declinedPurchase in allDeclinedPurchases)
                        {
                            declinedPurchase.DeclinedSubscriptionPurchase = null;
                            _unitOfWork.GetRepositoryAsync<Purchase>().Update(declinedPurchase.Id, declinedPurchase)
                                .GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during unmarking client declined subscription status");
                    }

                    return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");
                 
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e , $"{e.Message} in HandleCheckoutSessionCompletedEvent at {DateTime.UtcNow}.");

            }
            return OperationResult.Failure(
                $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Stripe.Checkout.Session).FullName}' type");
        }

        public async Task MoveRevenueFromEscrowAsync(string contributionId, string classId,
            List<string> participantsIds)
        {
            var clientsPurchases = (await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ContributionId == contributionId)).ToList();

            if (!clientsPurchases.Any())
            {
                return;
            }

            switch (clientsPurchases.First().ContributionType)
            {
                case nameof(ContributionOneToOne):

                    foreach (var purchase in clientsPurchases.Where(p => participantsIds.Contains(p.ClientId)))
                    {
                        foreach (var payment in purchase.Payments.Where(pm =>
                            pm.PaymentStatus == PaymentStatus.Succeeded
                            && pm.AffiliateRevenueTransfer != null
                            && pm.HasBookedClassId(classId)))
                        {
                            payment.AffiliateRevenueTransfer.IsInEscrow = false;
                        }

                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    break;
                case nameof(ContributionCourse):
                case nameof(ContributionCommunity):
                case nameof(ContributionMembership):
                    foreach (var purchase in clientsPurchases)
                    {
                        foreach (var payment in purchase.Payments.Where(pm =>
                            pm.PaymentStatus == PaymentStatus.Succeeded
                            && pm.AffiliateRevenueTransfer != null))
                        {
                            payment.AffiliateRevenueTransfer.IsInEscrow = false;
                        }

                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public async Task MoveParticipantsPaymentsFromEscrowAsync(
            string contributionId,
            string classId,
            List<string> participantsIds)
        {
            var clientsPurchases = (await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ContributionId == contributionId)).ToList();

            if (!clientsPurchases.Any())
            {
                return;
            }

            switch (clientsPurchases.First().ContributionType)
            {
                case nameof(ContributionOneToOne):

                    foreach (var purchase in clientsPurchases.Where(p => participantsIds.Contains(p.ClientId)))
                    {
                        foreach (var payment in purchase.Payments.Where(pm =>
                            pm.PaymentStatus == PaymentStatus.Succeeded && pm.HasBookedClassId(classId)))
                        {
                            payment.IsInEscrow = false;
                        }

                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    break;
                case nameof(ContributionCourse):

                    foreach (var purchase in clientsPurchases)
                    {
                        foreach (var payment in purchase.Payments.Where(pm =>
                            pm.PaymentStatus == PaymentStatus.Succeeded))
                        {
                            payment.IsInEscrow = false;
                        }

                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    break;
                case nameof(ContributionMembership):
                case nameof(ContributionCommunity):
                    //TODO: membership here
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public async Task<OperationResult> EnrollAcademyMembership(string contributionId, string accountId)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            return await TrySubscribeWithTrialSubscription(contribution, user);
        }

        public async Task<OperationResult> UpgradeMembershipPlan(
            string accountId,
            string contributionId,
            PaymentOptions newPaymentOption)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            var contribution =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            if (!(contribution is ContributionMembership || contribution is ContributionCommunity))
            {
                return OperationResult.Failure("Only membership allowed");
            }

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(e =>
                e.ContributionId == contribution.Id && e.ClientId == user.Id);

            if (purchase is null)
            {
                return OperationResult.Failure("Contribution not purchased yet");
            }

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (!purchaseVm.HasActiveSubscription)
            {
                return OperationResult.Failure("You have no active subscription");
            }
            var isMembership = contribution is ContributionMembership;
            BillingPlanInfo billingPlan = null;
            if (isMembership)
            {
                if (!((ContributionMembership)contribution).PaymentInfo.MembershipInfo.ProductBillingPlans.TryGetValue(
                    newPaymentOption,
                    out billingPlan))
                {
                    return OperationResult.Failure("Payment option is not available");
                }
            }
            else
            {
                if (!((ContributionCommunity)contribution).PaymentInfo.MembershipInfo.ProductBillingPlans.TryGetValue(
                    newPaymentOption,
                    out billingPlan))
                {
                    return OperationResult.Failure("Payment option is not available");
                }
            }

            if (purchaseVm.IsTrialSubscription)
            {
                return OperationResult.Failure("Free subscription can't be upgraded");
            }

            var upgradeSubscriptionResult =
                await _stripeService.UpgradeSubscriptionPlanAsync(
                    purchase.SubscriptionId,
                    billingPlan.ProductBillingPlanId);

            return upgradeSubscriptionResult.Failed ? upgradeSubscriptionResult : OperationResult.Success();
        }

        public async Task<OperationResult> CancelMembership(string accountId, string contributionId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            var contribution =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            if (!(contribution is ContributionMembership || contribution is ContributionCommunity))
            {
                return OperationResult.Failure("Only membership and community allowed");
            }

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(e =>
                e.ContributionId == contribution.Id && e.ClientId == user.Id);

            if (purchase is null)
            {
                return OperationResult.Failure("Contribution not purchased yet");
            }

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if (!purchaseVm.HasActiveSubscription)
            {
                return OperationResult.Failure("You have no active subscription");
            }

            var cancelSubscriptionResult = purchaseVm.IsTrialSubscription ?
                await _stripeService.CancelSubscriptionImmediately(purchaseVm.SubscriptionId) :
                await _stripeService.CancelSubscriptionAtPeriodEndAsync(purchaseVm.SubscriptionId);

            return cancelSubscriptionResult.Failed ? cancelSubscriptionResult : OperationResult.Success();
        }

        private SucceededPaymentEmailViewModel CalculateTotalPaymentAmount(
            ContributionBase contribution,
            Invoice fullInvoice,
            PaymentIntent paymentIntent,
            PurchasePayment payment,
            BalanceTransaction balanceTransaction)
        {
            if (fullInvoice != null)
            {
                //Only possible for membership
                return new SucceededPaymentEmailViewModel
                {
                    CurrentAmount = paymentIntent.Amount / _stripeService.SmallestCurrencyUnit,
                    ProcessingFee = fullInvoice.Charge.BalanceTransaction.Fee / _stripeService.SmallestCurrencyUnit,
                    PurchasePrice = fullInvoice.Charge.BalanceTransaction.Net / _stripeService.SmallestCurrencyUnit,
                    TotalAmount = fullInvoice.Charge.BalanceTransaction.Amount / _stripeService.SmallestCurrencyUnit,
                    PaymentOption = payment.PaymentOption,
                };
            }

            var paymentInfo = contribution.PaymentInfo;
            var currentAmount = paymentIntent.Amount / _stripeService.SmallestCurrencyUnit;
            var totalAmount = currentAmount;
            decimal processingFee;
            decimal purchasePrice;

            if (paymentInfo.Cost==null)
            {
                processingFee = paymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice.Value - currentAmount;
                purchasePrice = paymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice.Value;
            }
            else
            {
                processingFee = paymentInfo.Cost.Value - currentAmount;
                purchasePrice = paymentInfo.Cost.Value;
            }

            if (payment.PaymentOption == PaymentOptions.SplitPayments)
            {
                totalAmount = currentAmount * paymentInfo.SplitNumbers ?? 0;
                var pureSpitAmount = contribution.PaymentInfo.BillingPlanInfo.BillingPlanPureCost;
                processingFee = currentAmount - pureSpitAmount;
            }

            if (payment.PaymentOption == PaymentOptions.PerSession)
            {
                var classesCount = ResolveOneToOneAvailabilities(paymentIntent)?.SelectMany(a => a.Value).Count() ?? 0;
                purchasePrice = paymentInfo.Cost.Value * classesCount;
                processingFee = currentAmount - purchasePrice;
            }

            if (payment.PaymentOption == PaymentOptions.SessionsPackage)
            {
                purchasePrice = (paymentInfo.PackageCost) ?? (paymentInfo.Cost * paymentInfo.PackageSessionNumbers) ?? 0;

                if (contribution.PaymentInfo.PackageSessionDiscountPercentage.HasValue &&
                    contribution.PaymentInfo.PackageSessionDiscountPercentage > 0)
                {
                    var discount = (100m - paymentInfo.PackageSessionDiscountPercentage.Value) / 100m;
                    purchasePrice *= discount;
                }

                processingFee = currentAmount - purchasePrice;
            }

            if (payment.PaymentOption == PaymentOptions.EntireCourse)
            {
                purchasePrice = paymentInfo.Cost.Value;

                if (contribution.PaymentInfo.PackageSessionDiscountPercentage.HasValue &&
                    contribution.PaymentInfo.PackageSessionDiscountPercentage > 0)
                {
                    var discount = (100m - paymentInfo.PackageSessionDiscountPercentage.Value) / 100m;
                    purchasePrice *= discount;
                }

                processingFee = currentAmount - purchasePrice;
            }

            if (balanceTransaction != null)
            {
                currentAmount = balanceTransaction.Amount / _stripeService.SmallestCurrencyUnit;
                purchasePrice = balanceTransaction.Net / _stripeService.SmallestCurrencyUnit;
                processingFee = balanceTransaction.Fee / _stripeService.SmallestCurrencyUnit;
            }

            var result = new SucceededPaymentEmailViewModel
            {
                TotalAmount = totalAmount,
                CurrentAmount = currentAmount,
                ProcessingFee = processingFee,
                PurchasePrice = purchasePrice,
                PaymentOption = payment.PaymentOption,
            };

            return result;
        }

        private bool ResolveCurrentContribution(
            PaymentIntent paymentIntent,
            out ContributionBase contribution,
            string stripeAccountId = null)
        {
            if (!paymentIntent.Metadata.TryGetValue(
                Constants.Contribution.Payment.MetadataIdKey,
                out var contributionId))
            {
                var options = new InvoiceGetOptions();
                options.AddExpand("subscription.plan");
                var invoice = _invoiceService.Get(paymentIntent.InvoiceId, options, _stripeService.GetStandardAccountRequestOption(stripeAccountId));
                contributionId = invoice?.Subscription?.Plan?.ProductId;
            }

            contribution = _contributionRootService.GetOne(contributionId).GetAwaiter().GetResult();
            return contribution != null;
        }

        private OperationResult<PaymentIntent> HandleOneToOneBooking(PaymentIntent paymentIntent, ContributionBase contribution, User client)
        {
            if (contribution is ContributionOneToOne)
            {
                if (paymentIntent.Metadata.TryGetValue(
                                Constants.Contribution.Payment.BookOneToOneTimeViewModel,
                                out var bookOneToOneTimeViewModel))
                {
                    var serrializedBookOneToOneTimeViewModel = JsonConvert.DeserializeObject<BookOneToOneTimeViewModel>(bookOneToOneTimeViewModel);
                    if (serrializedBookOneToOneTimeViewModel != null)
                    {
                        var bookingResult = BookOneToOneTimeAsync(serrializedBookOneToOneTimeViewModel, client.AccountId).GetAwaiter().GetResult();

                        if (bookingResult.Failed)
                        {
                            return OperationResult<PaymentIntent>.Failure(bookingResult.Message);
                        }

                        var bookedTimes = (BookOneToOneTimeResultViewModel)bookingResult.Payload;
                        var bookedClassesIds = bookedTimes.AvailabilityTimeIdBookedTimeIdPairs.SelectMany(t => t.Value).ToList();

                        paymentIntent.Metadata.TryAdd(Constants.Contribution.Payment.AvailabilityTimeIdBookedTimeIdPairsKey, JsonConvert.SerializeObject(bookedTimes.AvailabilityTimeIdBookedTimeIdPairs));

                        // todo: remove if we don't need it
                        var classesCount = bookedTimes.AvailabilityTimeIdBookedTimeIdPairs.SelectMany(x => x.Value).Count();

                        return OperationResult<PaymentIntent>.Success(paymentIntent);
                    }
                }
            }
            return OperationResult<PaymentIntent>.Failure("Couldn't book one to one session");
        }

        private OperationResult HandleOneToOneContributionPurchaseEvent(
            ContributionOneToOne contribution,
            PaymentIntent paymentIntent,
            PurchasePayment payment,
            User client)
        {
            OperationResult oneToOneHandleResult;
            if (payment.PaymentOption == PaymentOptions.PerSession)
            {
                oneToOneHandleResult =
                    HandleOneToOneContributionPerSessionPurchaseEvent(contribution, paymentIntent, payment, client);
            }
            else if (payment.PaymentOption == PaymentOptions.SessionsPackage)
            {
                oneToOneHandleResult =
                    HandleOneToOneContributionSessionsPackagePurchaseEvent(contribution, paymentIntent, payment);
            }
            else if (payment.PaymentOption == PaymentOptions.MonthlySessionSubscription)
            {
                oneToOneHandleResult =
                    HandleOneToOneContributionMonthlySessionSubscriptionPurchaseEvent(contribution, paymentIntent,
                        payment, client);
            }
            else
            {
                oneToOneHandleResult =
                    OperationResult.Failure($"Unsupported payment option {payment.PaymentOption.ToString()}");
            }

            return oneToOneHandleResult;
        }

        private OperationResult HandleOneToOneContributionMonthlySessionSubscriptionPurchaseEvent(
            ContributionOneToOne contribution,
            PaymentIntent paymentIntent,
            PurchasePayment payment,
            User client)
        {
            var currentPackage =
                contribution.PackagePurchases.FirstOrDefault(e => e.TransactionId == paymentIntent.Id);

            int sessionPackage = contribution.PaymentInfo.MonthlySessionSubscriptionInfo.SessionCount.Value;
            int subscriptionDuration = contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration.Value;

            if (currentPackage == null)
            {
                contribution.PackagePurchases.Add(new PackagePurchase()
                {
                    SessionNumbers = sessionPackage,
                    UserId = client.Id,
                    TransactionId = paymentIntent.Id,
                    IsConfirmed = true,
                    IsMonthlySessionSubscription = true,
                    SubscriptionDuration = subscriptionDuration
                });
            }
            else
            {
                currentPackage.SessionNumbers += sessionPackage;
                currentPackage.MonthsPaid++;
            }

            _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution).GetAwaiter()
                .GetResult();

            return OperationResult.Success(null);
        }

        private void HandleCourseContributionPurchaseEvent(
            ContributionCourse contribution,
            Purchase purchase,
            PurchasePayment payment,
            User client)
        {
            if (payment.PaymentStatus != PaymentStatus.Succeeded)
            {
                return;
            }

            if (purchase.Payments.Count(p => p.PaymentStatus == PaymentStatus.Succeeded) != 1)
            {
                return;
            }

            foreach (var session in contribution.Sessions)
            {
                if (session.IsPrerecorded)
                {
                    foreach (var sessionTime in session.SessionTimes)
                    {
                        var note = new Note
                        {
                            UserId = client.Id,
                            ClassId = session.Id,
                            ContributionId = contribution.Id,
                            Title = session.Title,
                            SubClassId = sessionTime.Id,
                            IsPrerecorded = true
                        };
                        _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    var note = new Note
                    {
                        UserId = client.Id,
                        ClassId = session.Id,
                        ContributionId = contribution.Id,
                        Title = session.Title,
                    };

                    _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();
                }
            }
        }

        private Dictionary<string, IEnumerable<string>> ResolveOneToOneAvailabilities(PaymentIntent paymentIntent)
        {
            if (paymentIntent.Metadata.TryGetValue(
                Constants.Contribution.Payment.AvailabilityTimeIdBookedTimeIdPairsKey,
                out var purchaseAvailabilitiesJson))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, IEnumerable<string>>>(
                    purchaseAvailabilitiesJson);
            }

            return null;
        }

        private OperationResult HandleOneToOneContributionPerSessionPurchaseEvent(
            ContributionOneToOne contribution,
            PaymentIntent paymentIntent,
            PurchasePayment payment,
            User client)
        {
            var purchaseAvailabilities = ResolveOneToOneAvailabilities(paymentIntent);

            if (purchaseAvailabilities is null)
            {
                return OperationResult.Failure("User booked times are not specified");
            }

            var bookedTimePairs = contribution.AvailabilityTimes.Join(purchaseAvailabilities, x => x.Id, y => y.Key,
                    (x, y) => new { AllBookedTimes = x.BookedTimes, BookedTimes = y.Value })
                .ToList();

            if (payment.PaymentStatus == PaymentStatus.Succeeded)
            {
                var cohealer = _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId)
                    .GetAwaiter().GetResult();

                var confirmedBookedTimes = new List<string>();
                foreach (var bookedTimePair in bookedTimePairs)
                {
                    foreach (var bookedTime in bookedTimePair.AllBookedTimes.Where(t =>
                        bookedTimePair.BookedTimes.Contains(t.Id)))
                    {
                        bookedTime.IsPurchaseConfirmed = true;
                        confirmedBookedTimes.Add(bookedTime.Id);

                        var note = new Note
                        {
                            UserId = client.Id,
                            ClassId = bookedTime.Id,
                            ContributionId = contribution.Id,
                            Title = $"Session {bookedTime.SessionIndex}",
                        };

                        _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();

                        note.Title = $"Session {bookedTime.SessionIndex}";
                        note.UserId = cohealer.Id;
                        note.Id = null;

                        _unitOfWork.GetRepositoryAsync<Note>().Insert(note).GetAwaiter().GetResult();
                    }
                }

                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution).GetAwaiter()
                    .GetResult();

                if (confirmedBookedTimes.Count > 0)
                {
                    NotifyClientAndCoachAboutBookedSessions($"{client.FirstName} {client.LastName}", client.Id,
                            contribution, confirmedBookedTimes)
                        .GetAwaiter().GetResult();
                }
            }

            if (payment.PaymentStatus == PaymentStatus.Canceled)
            {
                foreach (var bookedTimePair in bookedTimePairs)
                {
                    bookedTimePair.AllBookedTimes.RemoveAll(t => bookedTimePair.BookedTimes.Contains(t.Id));
                }

                payment.BookedClassesIds.Clear();
                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution).GetAwaiter()
                    .GetResult();
            }

            return OperationResult.Success(null);
        }

        private OperationResult HandleOneToOneContributionSessionsPackagePurchaseEvent(
            ContributionOneToOne contribution, PaymentIntent paymentIntent, PurchasePayment payment)
        {
            var package = contribution.PackagePurchases.First(p => p.TransactionId == paymentIntent.Id);

            if (payment.PaymentStatus == PaymentStatus.Succeeded)
            {
                package.IsConfirmed = true;
                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution).GetAwaiter()
                    .GetResult();
            }

            if (payment.PaymentStatus == PaymentStatus.Canceled)
            {
                //contribution.PackagePurchases.Remove(package);
                package.IsConfirmed = false;
                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution).GetAwaiter()
                    .GetResult();
            }

            return OperationResult.Success(null);
        }

        private async Task<OperationResult> BookOneToOneTimeAsync(
            BookOneToOneTimeViewModel bookVm,
            string requesterAccountId)
        {
            var requesterUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requesterAccountId);

            var existingContribution = await _contributionRootService.GetOne(bookVm.ContributionId);

            if (existingContribution == null)
                return OperationResult.Failure("Contribution to assign user is not found");
            if (!(existingContribution is ContributionOneToOne contributionOneToOne))
                return OperationResult.Failure(
                    "Unable to book one to one time. Contribution which Id was provided is not One-To-One type");
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(e => e.Id == contributionOneToOne.UserId);

            var oneToOneVm = _mapper.Map<ContributionOneToOneViewModel>(contributionOneToOne);
            var slots = await _contributionRootService.GetAvailabilityTimesForClient(oneToOneVm.Id,
                requesterAccountId, bookVm.Offset, string.Empty, timesInUtc: true);
            var result =
                oneToOneVm.AssignUserToContributionTime(bookVm, requesterUser.Id, slots, coachUser.TimeZoneId , coachUser.AccountId);

            if (!result.Succeeded)
            {
                return OperationResult.Failure(result.Message);
            }
            else
            {
                if (existingContribution.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom)
                {
                    var booktime = oneToOneVm.AvailabilityTimes.Where(x => x.Id == bookVm.AvailabilityTimeId).FirstOrDefault().BookedTimes.LastOrDefault();
                    if (booktime != null)
                    {
                        //Add meeting object with bookedtime
                        try
                        {
                            var meeting = await _zoomService.ScheduleMeetingForOneToOne("Session", booktime.EndTime, booktime.StartTime, coachUser);
                            booktime.ZoomMeetingData = new ZoomMeetingData
                            {
                                MeetingId = meeting.Id,
                                JoinUrl = meeting.JoinUrl,
                                StartUrl = meeting.StartUrl
                            };
                            oneToOneVm.ZoomMeetigsIds.Add(meeting.Id);
                        }
                        catch (Exception ex)
                        {
                            Console.Write(ex.Message);
                        }
                    }
                }
                
            }
            var updatedOneToOne = _mapper.Map<ContributionOneToOne>(oneToOneVm);
            await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .Update(contributionOneToOne.Id, updatedOneToOne);
            return OperationResult.Success("User has booked availability time(s) successfully",
                result.Payload);
        }

        private OperationResult<CreateMoneyTransferResult> CreateMoneyTransfer(
            PaymentIntent paymentIntent,
            ContributionBase contribution,
            long? transferAmountLong, decimal? purchaseAmount)
        {
            var contributorUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId)
                .GetAwaiter().GetResult();
            var contributorAccount = _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == contributorUser.AccountId).GetAwaiter().GetResult();
            var charge = paymentIntent.Charges.First(x => x.Status == "succeeded");

            var currentPaidTier = _paidTiersService.GetCurrentPaidTier(contributorAccount.Id).GetAwaiter().GetResult();

            var connectedStripeAccountId = contributorUser.ConnectedStripeAccountId;

            var existingTransfer = _payoutService.GetTransferAsync(charge.Id, connectedStripeAccountId).GetAwaiter()
                .GetResult();

            if (existingTransfer != null)
            {
                if (contributorUser.IsBetaUser)
                {
                    var difference = (purchaseAmount - transferAmountLong) - (purchaseAmount - existingTransfer.Amount);
                    if(difference > 0) ReverseTransferForBetaCoach(charge, Convert.ToInt64(difference));
                }
                return HandleExistedTransfer(contributorAccount, charge, existingTransfer);
            }


            TransferMoneyViewModel transferMoneyViewModel = null;
            if (paymentIntent.Metadata.TryGetValue(
                Constants.Contribution.Payment.TransferMoneyDataKey,
                out var transferMoneyDataKey))
            {
                transferMoneyViewModel = JsonConvert.DeserializeObject<TransferMoneyViewModel>(transferMoneyDataKey);
            }

            // TODO: check if we want to override with those values for old transactions that will have transferAmount of 0
            var serviceProviderIncome = transferMoneyViewModel?.TransferAmount ?? 0;
            var purchaseAmountFromMeta = transferMoneyViewModel?.PurchaseAmount ?? 0;

            return CreateTransfers(connectedStripeAccountId, contributorAccount, charge, currentPaidTier.PaidTierOption, transferAmountLong ?? serviceProviderIncome,
                purchaseAmount ?? purchaseAmountFromMeta);
        }
        private void ReverseTransferForBetaCoach(Charge charge,long difference)
        {
            if (!string.IsNullOrEmpty(charge.TransferId))
            {
                var options = new TransferReversalCreateOptions
                {
                    Amount = difference,
                };

                var service = new TransferReversalService();
                var reversal = service.Create(charge.TransferId, options);
            }
        }
        /// <summary>
        /// Transfers money from payment intent generated by subscription to the service provider
        /// </summary>
        /// <param name="paymentIntent">Automatically generate stripe payment intent</param>
        /// <param name="contribution">Contribution</param>
        /// <returns></returns>
        private OperationResult<CreateMoneyTransferResult> CreateSubscriptionMoneyTransfer(
            PaymentIntent paymentIntent,
            ContributionBase contribution)
        {
            var contributorUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId)
                .GetAwaiter().GetResult();
            var contributorAccount = _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(x => x.Id == contributorUser.AccountId).GetAwaiter().GetResult();
            var charge = paymentIntent.Charges.First(x => x.Status == "succeeded");

            var connectedStripeAccountId = contributorUser.ConnectedStripeAccountId;

            var existingTransfer = _payoutService.GetTransferAsync(charge.Id, connectedStripeAccountId).GetAwaiter()
                .GetResult();

            if (existingTransfer != null)
            {
                return HandleExistedTransfer(contributorAccount, charge, existingTransfer);
            }

            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments) &&
                !contribution.PaymentInfo.SplitNumbers.HasValue)
            {
                return OperationResult<CreateMoneyTransferResult>.Failure(
                    "Contribution split numbers are not specified");
            }

            var serviceProviderIncome = decimal.ToInt64(contribution.PaymentInfo.BillingPlanInfo.BillingPlanTransferCost
                                                        * _stripeService.SmallestCurrencyUnit);

            var totalAmount = contribution.PaymentInfo.BillingPlanInfo.BillingPlanPureCost
                              * _stripeService.SmallestCurrencyUnit;

            var currentPaidTierViewModel = _paidTiersService.GetCurrentPaidTier(contributorAccount.Id).GetAwaiter().GetResult();

            return CreateTransfers(
                connectedStripeAccountId,
                contributorAccount,
                charge,
                currentPaidTierViewModel.PaidTierOption,
                serviceProviderIncome,
                totalAmount);
        }

        public OperationResult<CreateMoneyTransferResult> CreateTransfers(
            string connectedStripeAccountId,
            Account contributorAccount,
            Charge charge,
            PaidTierOption paidTierOption,
            long serviceProviderIncome,
            decimal? totalAmount)
        {
            if (contributorAccount.InvitedBy == null)
            {
                var transfer = _payoutService
                    .CreateTransferAsync(charge.Id, connectedStripeAccountId, serviceProviderIncome,charge.Currency).GetAwaiter()
                    .GetResult();

                return new OperationResult<CreateMoneyTransferResult>(
                    transfer.Succeeded,
                    transfer.Message,
                    new CreateMoneyTransferResult()
                    {
                        CoachTransfer = (Transfer)transfer.Payload,
                    });
            }
            else
            {
                var availableAffiliateRevenue = _affiliateCommissionService
                    .GetAffiliateIncomeAsLong(totalAmount.Value, paidTierOption.NormalizedFee, contributorAccount.Id).GetAwaiter().GetResult();

                if (availableAffiliateRevenue > 0L)
                {
                    var affiliateUser = _unitOfWork.GetRepositoryAsync<User>()
                        .GetOne(e => e.AccountId == contributorAccount.InvitedBy).GetAwaiter().GetResult();

                    var affiliateConnectedStripeAccountId = affiliateUser.ConnectedStripeAccountId;

                    return _payoutService.CreateGroupTransferAsync(
                        charge.Id,
                        connectedStripeAccountId,
                        serviceProviderIncome,
                        affiliateConnectedStripeAccountId,
                        availableAffiliateRevenue).GetAwaiter().GetResult();
                }

                var transfer = _payoutService
                    .CreateTransferAsync(charge.Id, connectedStripeAccountId, serviceProviderIncome,charge.Currency).GetAwaiter()
                    .GetResult();

                return new OperationResult<CreateMoneyTransferResult>(
                    transfer.Succeeded,
                    transfer.Message,
                    new CreateMoneyTransferResult()
                    {
                        CoachTransfer = (Transfer)transfer.Payload,
                    });
            }
        }

        public async Task<OperationResult> PurchaseLiveCourseWithCheckout(string contributionId, string purchaseId, string requesterAccountId, PaymentOptions paymentOption, string couponId, string accessCode)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            if (!(contribution is ContributionCourse contributionCourse))
            {
                return OperationResult.Failure("only live courses supported");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOption) && paymentOption != PaymentOptions.Free)
            {
                return OperationResult.Failure("not supported payment option");
            }

            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == requesterAccountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == requesterAccountId);
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == coachUser.AccountId);

            // check if 100% coupon code applies here
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased grouped contribution through 100% off coupon", purchaseResult.Payload);
                    }
                }
            }

            // check if session payment option is free
            if (paymentOption == PaymentOptions.Free)
            {
                var isAccessCodeValid = validateAccessCode(accessCode, contributionId);
                if (isAccessCodeValid)
                {
                    var purchaseResult = await PurchaseSessionBasedContributionFreeWithoutCheckout(contribution, clientUser.Id, couponId, paymentOption);
                    if (purchaseResult.Succeeded)
                    {
                        return OperationResult.Success("Purchased Free grouped contribution with Access Code", purchaseResult.Payload);
                    }
                }
            }

            if (contribution.PaymentType == PaymentTypes.Advance && (!coachUser.IsStandardAccount || string.IsNullOrEmpty(coachUser.StripeStandardAccountId)))
            {
                return OperationResult.Failure("unsupported payment type for contribtuion", "unsupported payment type for contribtuion. Advance payment is enable for the Stripe standard account only");
            }

            if (paymentOption == PaymentOptions.EntireCourse)
            {
                var priceResult = await GetPriceForProductPaymentOptionAsync(contributionCourse, paymentOption, couponId);
                if (priceResult.Failed)
                {
                    return priceResult;
                }
                var (priceId, cost) = priceResult.Payload;


                //fee calculation
                var country = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == coachUser.CountryId);
                var dynamicStripeFee = await _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Alpha2Code);

                //get or create customerId available for the respective coach standar account
                var customerResult = await GetOrCreateCustomer(clientUser, clientAccount.Email, coachUser.StripeStandardAccountId, contribution.PaymentType, contribution.DefaultCurrency);
                if (customerResult.Failed)
                {
                    return customerResult;
                }

                var sessionModel = new CreateCheckoutSessionModel()
                {
                    TotalChargedCost = cost,
                    StripeFee = dynamicStripeFee?.Fee ?? 2.9M,
                    FixedStripeAmount = dynamicStripeFee?.Fixed ?? 0.30M,
                    InternationalFee = dynamicStripeFee?.International ?? 3.9M,
                    ProductCost = contribution.PaymentInfo.Cost,
                    DiscountPercent = contribution.PaymentInfo.PackageSessionDiscountPercentage,
                    CoachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee,
                    ServiceAgreementType = coachUser.ServiceAgreementType,
                    StripeCustomerId = customerResult.Payload as string,
                    ContributionId = contribution.Id,
                    PaymentOption = paymentOption,
                    PurchaseId = purchaseId,
                    PriceId = priceId,
                    CouponId = couponId,
                    ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                    StripeStandardAccountId = coachUser.StripeStandardAccountId,
                    IsStandardAccount = coachUser.IsStandardAccount,
                    paymentType = contribution.PaymentType,
                    ClientEmail = clientAccount.Email,
                    ClientFirstName = clientUser.FirstName,
                    ClientLastName = clientUser.LastName,
                    CoachEmail = coachAccount.Email,
                    ContributionTitle = contribution.Title,
                    TaxType= contribution.TaxType
                };
                if (couponId != null)
                {
                    var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                    sessionModel.CouponPerecent = validateCouponResult?.PercentAmount;
                }
                var result = await _stripeService.CreateCheckoutSessionSinglePayment(sessionModel);
                if (result.Succeeded)
                {
                    if (contribution.PaymentType == PaymentTypes.Advance)
                    {
                        return OperationResult<string>.Success(String.Empty, (string)result.Payload.RawJObject["url"]);
                    }
                    return OperationResult.Success(String.Empty, result.Payload.Id);
                }
                else
                {
                    return OperationResult<string>.Failure(result.Message);
                } 
                
            }

            if (paymentOption == PaymentOptions.SplitPayments)
            {
                //get or create customerId available for the respective coach standar account
                var customerResult = await GetOrCreateCustomer(clientUser, clientAccount.Email, coachUser.StripeStandardAccountId, contribution.PaymentType, contribution.DefaultCurrency);
                if (customerResult.Failed)
                {
                    return customerResult;
                }

                var result = await _stripeService.CreateSubscriptionCheckoutSession(new CreateCheckoutSessionModel()
                {
                    ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                    StripeStandardAccountId = coachUser.StripeStandardAccountId,
                    IsStandardAccount = coachUser.IsStandardAccount,
                    paymentType = contribution.PaymentType,
                    ServiceAgreementType = coachUser.ServiceAgreementType,
                    ContributionId = contribution.Id,
                    PaymentOption = paymentOption,
                    StripeCustomerId = customerResult.Payload as string,
                    PurchaseId = purchaseId,
                    PriceId = contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId,
                    BillingInfo = contribution.PaymentInfo.BillingPlanInfo,
                    CouponId = couponId,
                    SplitNumbers = contribution?.PaymentInfo?.SplitNumbers,
                    ClientEmail = clientAccount.Email,
                    ClientFirstName = clientUser.FirstName,
                    ClientLastName = clientUser.LastName,
                    CoachEmail = coachAccount.Email,
                    ContributionTitle = contribution.Title,
                    TaxType = contribution.TaxType
                });
                if (result.Succeeded)
                {
                    if (contribution.PaymentType == PaymentTypes.Advance)
                    {
                        return OperationResult<string>.Success(String.Empty, (string)result.Payload.RawJObject["url"]);
                    }
                    return OperationResult.Success(String.Empty, result.Payload.Id);
                }
                else
                {
                    return OperationResult<string>.Failure(result.Message);
                }
            }

            throw new NotImplementedException();
        }

        public async Task<OperationResult> PurchaseLiveCourseWithInvoice(string contributionId, string purchaseId, string requesterAccountId, PaymentOptions paymentOption, 
            string couponId)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            if (!(contribution is ContributionCourse contributionCourse))
            {
                return OperationResult.Failure("only live courses supported");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOption))
            {
                return OperationResult.Failure("not supported payment option");
            }

            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == requesterAccountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == clientUser.AccountId);
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);

            if (contribution.PaymentType != PaymentTypes.Advance || !coachUser.IsStandardAccount)
            {
                return OperationResult<Invoice>.Failure("Invoice is currently available for advance payment for standard account");
            }

            var invoiceExisted = _commonService.GetInvoiceIfExist(clientUser.Id, contributionId, paymentOption.ToString());
            if (invoiceExisted is not null)
            {
                return OperationResult<string>.Success(invoiceExisted.InvoiceId);
            }

            // check if 100% coupon code applies here
            if (couponId != null)
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                if (validateCouponResult?.PercentAmount == 100)
                {
                    var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(x => x.ContributionId == contributionCourse.Id && x.ClientId == clientUser.Id);
                    var payment = new PurchasePayment()
                    {
                        PaymentStatus = PaymentStatus.Succeeded,
                        DateTimeCharged = DateTime.UtcNow,
                        PaymentOption = paymentOption,
                        GrossPurchaseAmount = 0,
                        TransferAmount = 0,
                        ProcessingFee = 0,
                        IsInEscrow = !contributionCourse.InvitationOnly,
                        PurchaseCurrency = contribution.DefaultCurrency,
                        Currency = contribution.DefaultCurrency
                    };
                    if (clientPurchase == null)
                    {
                        clientPurchase = new Purchase()
                        {
                            ClientId = clientUser.Id,
                            ContributorId = contributionCourse.UserId,
                            ContributionId = contributionCourse.Id,
                            Payments = new List<PurchasePayment>() { payment },
                            ContributionType = contributionCourse.Type,
                            CouponId = couponId
                        };
                    }
                    // todo: check if we need to have a condition here before entering it
                    else
                    {
                        clientPurchase.Payments.Add(payment);
                        clientPurchase.CouponId = couponId;
                    }

                    if (clientPurchase.Id is null)
                    {
                        await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
                    }
                    else
                    {
                        _synchronizePurchaseUpdateService.Sync(clientPurchase);
                    }

                    HandleCourseContributionPurchaseEvent(contributionCourse, clientPurchase, payment, clientUser);

                    try
                    {
                        AfterSave(contributionCourse, clientPurchase, payment, clientUser);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
                    }

                    await _notificationService.SendPaymentSuccessNotification(clientPurchase, contribution, null);
                    return OperationResult<string>.Success("100discount");
                }
            }

            if (!string.IsNullOrEmpty(clientUser.CountryId))
            {
                var CountryAlpha2Code = _unitOfWork.GetRepositoryAsync<Country>().GetOne(c => c.Id == clientUser.CountryId).GetAwaiter().GetResult().Alpha2Code;
                var customerResult = await GetOrCreateCustomerForInvoice(clientUser, clientAccount.Email, coachUser.StripeStandardAccountId, contribution.PaymentType, contribution.DefaultCurrency, CountryAlpha2Code);
                if (customerResult.Failed)
                {
                    return customerResult;
                }

                if (paymentOption == PaymentOptions.EntireCourse)
                {
                    var priceResult = await GetPriceForProductPaymentOptionAsync(contributionCourse, paymentOption, couponId);
                    if (priceResult.Failed)
                    {
                        return priceResult;
                    }
                    var (priceId, cost) = priceResult.Payload;
                    //fee calculation
                    var country = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == coachUser.CountryId);
                    var dynamicStripeFee = await _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Alpha2Code);

                    var sessionModel = new CreateCheckoutSessionModel()
                    {
                        TotalChargedCost = cost,
                        StripeFee = dynamicStripeFee?.Fee ?? 2.9M,
                        FixedStripeAmount = dynamicStripeFee?.Fixed ?? 0.30M,
                        InternationalFee = dynamicStripeFee?.International ?? 3.9M,
                        ProductCost = contribution.PaymentInfo.Cost,
                        DiscountPercent = contribution.PaymentInfo.PackageSessionDiscountPercentage,
                        CoachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee,
                        ServiceAgreementType = coachUser.ServiceAgreementType,
                        StripeCustomerId = customerResult.Payload as string,
                        ContributionId = contribution.Id,
                        PaymentOption = paymentOption,
                        PurchaseId = purchaseId,
                        PriceId = priceId,
                        CouponId = couponId,
                        ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                        paymentType = contribution.PaymentType,
                        IsStandardAccount = coachUser.IsStandardAccount,
                        StripeStandardAccountId = coachUser.StripeStandardAccountId,
                        Currency = contribution.DefaultCurrency,
                        ClientId = clientUser.Id,
                        TaxType = contribution.TaxType
                    };

                    if (couponId != null)
                    {
                        var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                        sessionModel.CouponPerecent = validateCouponResult?.PercentAmount;
                    }

                    var result = await _stripeService.CreateInvoiceForSinglePayment(sessionModel);
                    if (result.Succeeded)
                    {
                        await _notificationService.SendInvoiceDueEmailToClient(clientAccount.Email, clientUser.FirstName, contribution.Title, coachUser.FirstName);

                        await _notificationService.SendInvoiceDueEmailToCoach(coachUser.AccountId, clientAccount.Email, result.Payload.Number, contribution.Title);

                        return OperationResult<string>.Success(result.Payload.Id);
                    }

                    return OperationResult<string>.Failure(result.Message);
                }

                if (paymentOption == PaymentOptions.SplitPayments)
                {
                    var result = await _stripeService.CreateInvoiceForSubscription(new CreateCheckoutSessionModel()
                    {
                        ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                        ServiceAgreementType = coachUser.ServiceAgreementType,
                        ContributionId = contribution.Id,
                        PaymentOption = paymentOption,
                        StripeCustomerId = customerResult.Payload as string,
                        PurchaseId = purchaseId,
                        PriceId = contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId,
                        BillingInfo = contribution.PaymentInfo.BillingPlanInfo,
                        CouponId = couponId,
                        SplitNumbers = contribution?.PaymentInfo?.SplitNumbers,
                        paymentType = contribution.PaymentType,
                        IsStandardAccount = coachUser.IsStandardAccount,
                        StripeStandardAccountId = coachUser.StripeStandardAccountId,
                        ClientId = clientUser.Id,
                        TaxType = contribution.TaxType
                    });

                    if (result.Succeeded)
                    {
                        await _notificationService.SendInvoiceDueEmailToClient(clientAccount.Email, clientUser.FirstName, contribution.Title, coachUser.FirstName);
                        return OperationResult<string>.Success(result.Payload.Id);
                    }

                    return OperationResult<string>.Failure(result.Message);
                } 
            }
            return OperationResult.Failure("Country is not saved.");
        }

        private async Task<OperationResult> GetOrCreateCustomer(User clientUser, string customerEmail, string stripeStandardAccount, PaymentTypes paymentType, string contributionCurrency)
        {
            if (paymentType != PaymentTypes.Advance)
            {
                stripeStandardAccount = null;
            }

            var customerAccountsList = _stripeAccountService.GetCustomerAccountList(customerEmail, stripeStandardAccount).Payload as List<StripeCustomerAccount>;
            if (customerAccountsList.Count > 0)
            {                  
                var customerAccount = customerAccountsList?.FirstOrDefault(c => c.Currency == contributionCurrency || string.IsNullOrEmpty(c.Currency));
                if (customerAccount != null)
                {
                    if (customerAccount.CustomerId == clientUser.CustomerStripeAccountId)
                    {
                        return OperationResult.Success(String.Empty, customerAccount.CustomerId);
                    }
                    clientUser.CustomerStripeAccountId = customerAccount.CustomerId;
                    await _unitOfWork.GetRepositoryAsync<User>().Update(clientUser.Id, clientUser);
                    return OperationResult.Success(string.Empty, customerAccount.CustomerId);
                }
            }

            var newCustomer = await _stripeAccountService.CreateCustomerAsync(customerEmail, createNew: true, stripeStandardAccount);  //create new sets to true to  create another customer for different currency in the same account
            if (newCustomer.Succeeded)
            {
                clientUser.CustomerStripeAccountId = newCustomer.Payload;
                await _unitOfWork.GetRepositoryAsync<User>().Update(clientUser.Id, clientUser);
                return OperationResult.Success(string.Empty, newCustomer.Payload);
            }

            return OperationResult.Failure(newCustomer.Message);
        }
        static SemaphoreSlim semaphoreAsyncLock = new SemaphoreSlim(1, 1);
        private async Task<OperationResult> GetOrCreateCustomerForInvoice(User clientUser, string customerEmail, string stripeStandardAccount, PaymentTypes paymentType, string contributionCurrency,
           string countryAlpha2Code)
        {
            var customerAccountsList = _stripeAccountService.GetCustomerAccountListForInvoice(customerEmail, stripeStandardAccount).Payload as List<Customer>;
            if (customerAccountsList?.Count > 0)
            {
                var customerAccount = customerAccountsList?.FirstOrDefault(c => c.Currency == contributionCurrency || string.IsNullOrEmpty(c.Currency));
                if (customerAccount != null)
                {
                    if (IsCustomerBillingAddressChangedForInvoice(customerAccount, countryAlpha2Code))
                    {
                        await _stripeAccountService.UpdateCustomerBillingInfoForTaxInInvoice(customerAccount.Id, stripeStandardAccount, countryAlpha2Code);
                    }

                    if (customerAccount.Id == clientUser.CustomerStripeAccountId)
                    {
                        return OperationResult.Success(String.Empty, customerAccount.Id);
                    }
                    clientUser.CustomerStripeAccountId = customerAccount.Id;
                    await _unitOfWork.GetRepositoryAsync<User>().Update(clientUser.Id, clientUser);
                    return OperationResult.Success(string.Empty, customerAccount.Id);
                }
            }

            var newCustomer = await _stripeAccountService.CreateCustomerAsync(customerEmail, createNew: true, stripeStandardAccount, countryAlpha2Code);  //create new sets to true to  create another customer for different currency in the same account
            if (newCustomer.Succeeded)
            {
                clientUser.CustomerStripeAccountId = newCustomer.Payload;
                await _unitOfWork.GetRepositoryAsync<User>().Update(clientUser.Id, clientUser);
                return OperationResult.Success(string.Empty, newCustomer.Payload);
            }
            return OperationResult.Failure(newCustomer.Message);
        }

        private bool IsCustomerBillingAddressChangedForInvoice(Customer customerAccount, string countryAlpha2Code)
        {
            var currentAddress = customerAccount.Address;

            // country and postal always has values so
            if (currentAddress is null)
            {
                return true;
            }

            if (currentAddress.Country != countryAlpha2Code)
            {
                return true;
            }

            return false;
        }
        private async Task<OperationResult> PurchaseSessionBasedContributionFreeWithoutCheckout(ContributionBase contribution, string userId, string couponId, PaymentOptions paymentOption)
        {
            await semaphoreAsyncLock.WaitAsync();

            try
            {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == user.Id);
                if (clientPurchase != null)
                {
                    if (clientPurchase.Payments.Count > 0 && clientPurchase.Payments.LastOrDefault()?.TransactionId?.StartsWith("100_off_") == true)
                    {
                        return OperationResult<string>.Success("100discount", "Free Session Already Joined");
                    }
                }
            var payment = new PurchasePayment()
            {
                PaymentStatus = PaymentStatus.Succeeded,
                DateTimeCharged = DateTime.UtcNow,
                PaymentOption = paymentOption,
                GrossPurchaseAmount = 0,
                TransferAmount = 0,
                ProcessingFee = 0,
                IsInEscrow = !contribution.InvitationOnly,
                PurchaseCurrency = contribution.DefaultCurrency,
                Currency = contribution.DefaultCurrency,
                TransactionId = "100_off_" + Guid.NewGuid().ToString()
            };
            if (clientPurchase == null)
            {
                clientPurchase = new Purchase()
                {
                    ClientId = user.Id,
                    ContributorId = contribution.UserId,
                    ContributionId = contribution.Id,
                    Payments = new List<PurchasePayment>() { payment },
                    SubscriptionId = "-2", // 100% discount subscription
                    ContributionType = contribution.Type,
                    CouponId = couponId,
                    PaymentType = contribution.PaymentType.ToString(),
                    TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
                };
            }
            // todo: check if we need to have a condition here before entering it
            else
            {
                clientPurchase.Payments.Add(payment);
                clientPurchase.CouponId = couponId;
            }
            if (clientPurchase.Id is null)
            {
                await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
            }
            else
            {
                _synchronizePurchaseUpdateService.Sync(clientPurchase);
            }
            try
            {
                AfterSave(contribution, clientPurchase, payment, user);
                //Send Push Notification (Free Contribution)
                try
                {
                        await _fcmService.SendFreeGroupContributionJoinPushNotification(contribution.Id, user.Id).ConfigureAwait(false);
                }
                catch
                {
                }
                try
                {
                    //Send Email Notification to coach
                    var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == user.AccountId);
                        await _notificationService.SendClientFreeEnrolledNotification(contribution, clientAccount.Email, $"{user.FirstName} {user.LastName}").ConfigureAwait(false);
                    //END
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error during afterSave action in {nameof(HandlePaymentIntentStripeEvent)}");
            }
            return OperationResult<string>.Success("100discount", "Free session joined successfully");
        }
            finally 
            {
                semaphoreAsyncLock.Release();
            }
        }

        public async Task<OperationResult> PurchaseOneToOneWithCheckout(string contributionId, Dictionary<string, IEnumerable<string>> availabilityTimeIdBookedTimeIdPairsKey, string purchaseId,
            string requesterAccountId, PaymentOptions paymentOption, string couponId, string availabilityTimeId, BookOneToOneTimeViewModel bookOneToOneTimeViewModel)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.Id == contribution.UserId);
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == coachUser.AccountId);

            if (!(contribution is ContributionOneToOne contributionOneToOne))
            {
                return OperationResult.Failure("only one to one contribution supported");
            }

            if (!contribution.PaymentInfo.PaymentOptions.Contains(paymentOption))
            {
                return OperationResult.Failure("not supported payment option");
            }

            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == requesterAccountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requesterAccountId);
            
            var customerResult = await GetOrCreateCustomer(clientUser, clientAccount.Email, coachUser.StripeStandardAccountId, contribution.PaymentType, contribution.DefaultCurrency);
            if (customerResult.Failed)
            {
                return customerResult;
            }

            if (paymentOption == PaymentOptions.PerSession || paymentOption == PaymentOptions.SessionsPackage)
            {
                var priceResult = await GetPriceForProductPaymentOptionAsync(contributionOneToOne, paymentOption, couponId);
                if (priceResult.Failed)
                {
                    return priceResult;
                }
                var (priceId, cost) = priceResult.Payload;

                var country = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == coachUser.CountryId);
                var dynamicStripeFee = await _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Alpha2Code);
                var sessionModel = new CreateCheckoutSessionModel()
                {
                    TotalChargedCost = cost,
                    ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                    StripeFee = dynamicStripeFee?.Fee ?? 2.9M,
                    FixedStripeAmount = dynamicStripeFee?.Fixed ?? 0.30M,
                    InternationalFee = dynamicStripeFee?.International ?? 3.9M,
                    ProductCost = contribution.PaymentInfo.Cost,
                    DiscountPercent = contribution.PaymentInfo.PackageSessionDiscountPercentage,
                    CoachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee,
                    ServiceAgreementType = coachUser.ServiceAgreementType,
                    StripeCustomerId = customerResult.Payload as string,
                    ContributionId = contribution.Id,
                    PaymentOption = paymentOption,
                    //PurchaseId = purchaseId,
                    AvailabilityTimeIdBookedTimeIdPairsKey = availabilityTimeIdBookedTimeIdPairsKey,
                    CouponId = couponId,
                    PriceId = priceId,
                    AvailabilityTimeId = availabilityTimeId,
                    BookOneToOneTimeViewModel = bookOneToOneTimeViewModel,
                    StripeStandardAccountId = coachUser.StripeStandardAccountId,
                    paymentType = contribution.PaymentType,
                    IsStandardAccount = coachUser.IsStandardAccount,
                    ClientEmail = clientAccount.Email,
                    ClientFirstName = clientUser.FirstName,
                    ClientLastName = clientUser.LastName,
                    CoachEmail = coachAccount.Email,
                    ContributionTitle = contribution.Title,
                    TaxType = contribution.TaxType
                };

                if (couponId != null)
                {
                    var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contributionId, paymentOption);
                    sessionModel.CouponPerecent = validateCouponResult?.PercentAmount;
                }
                var result = await _stripeService.CreateCheckoutSessionSinglePayment(sessionModel);
                if (result.Succeeded)
                {
                    if (contribution.PaymentType == PaymentTypes.Advance)
                    {
                        return OperationResult<string>.Success(String.Empty, (string)result.Payload.RawJObject["url"]);
                    }
                    return OperationResult<Stripe.Checkout.Session>.Success(result.Payload);
                }
                else
                {
                    return OperationResult<string>.Failure(result.Message);
                } 
                
            }

            if (paymentOption == PaymentOptions.MonthlySessionSubscription)
            {
                var result = await _stripeService.CreateSubscriptionCheckoutSession(new CreateCheckoutSessionModel()
                {
                    ConnectedStripeAccountId = coachUser.ConnectedStripeAccountId,
                    ServiceAgreementType = coachUser.ServiceAgreementType,
                    ContributionId = contribution.Id,
                    PaymentOption = paymentOption,
                    StripeCustomerId = customerResult.Payload as string,
                    //PurchaseId = purchaseId,
                    PriceId = contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId,
                    BillingInfo = contribution.PaymentInfo.BillingPlanInfo,
                    CouponId = couponId,
                    AvailabilityTimeId = availabilityTimeId,
                    BookOneToOneTimeViewModel = bookOneToOneTimeViewModel,
                    StripeStandardAccountId = coachUser.StripeStandardAccountId,
                    paymentType = contribution.PaymentType,
                    IsStandardAccount = coachUser.IsStandardAccount,
                    ClientEmail = clientAccount.Email,
                    ClientFirstName = clientUser.FirstName,
                    ClientLastName = clientUser.LastName,
                    CoachEmail = coachAccount.Email,
                    ContributionTitle = contribution.Title,
                    TaxType = contribution.TaxType
                });

                if (result.Succeeded)
                {
                    if (contribution.PaymentType == PaymentTypes.Advance)
                    {
                        return OperationResult<string>.Success(String.Empty, (string)result.Payload.RawJObject["url"]);
                    }
                    return OperationResult<Stripe.Checkout.Session>.Success(result.Payload);
                }
                else
                {
                    return OperationResult<string>.Failure(result.Message);
                }
            }

            throw new NotImplementedException();
        }

        private async Task<string> GetOrCreateProductAsync(ContributionBase contribution, string standardAccountId = null)
        {
            try
            {
                var product = await _productService.GetAsync(contribution.Id,options: null, _stripeService.GetStandardAccountRequestOption(standardAccountId));
                return product.Id;
            }
            catch (StripeException ex)
            {
                if (ex.StripeError.Code != "resource_missing") throw;

                var product = await _stripeService.CreateProductAsync(new CreateProductViewModel()
                {
                    Id = contribution.Id,
                    Name = contribution.Title,
                    StandardAccountId = standardAccountId
                });

                return product.Payload;
            }
        }

        //TODO: refactor to prevent request limit error from stripe
        private async Task<OperationResult<Tuple<string,decimal>>> GetPriceForProductPaymentOptionAsync(ContributionBase contribution, PaymentOptions paymentOption, string couponId)
        {
            //Get connected stripe standard Account Id If enable
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            string standardAccountId = null;
            if (coachUser != null && coachUser.IsStandardAccount && contribution.PaymentType == PaymentTypes.Advance)
            {
                standardAccountId = coachUser.StripeStandardAccountId;
            }

            //TODO: create a tool to create product for existed contribution and save create product id at the contribution level
            var productId = await GetOrCreateProductAsync(contribution, standardAccountId);

            if (contribution?.PaymentInfo?.Cost is null)
            {
                _logger.LogError($"cost not exist for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
                return OperationResult<Tuple<string, decimal>>.Failure(
                    $"cost not exist for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
            }

            //TODO: add stripe fees - done (remove comment after test)
            //TODO: apply discount for full payment (live course full payment discount or one-to-one session package discount) - done (remove comment after test)
            OperationResult purchaseDetailsResult = null;
            if (contribution is ContributionCourse)
            {
                purchaseDetailsResult = await GetCourseContributionPurchaseDetailsAsync(contribution.Id, paymentOption, couponId);
            }
            else if (contribution is ContributionOneToOne)
            {
                purchaseDetailsResult = await GetOneToOneContributionPurchaseDetailsAsync(contribution.Id, paymentOption, couponId);
            }
            else if (contribution is ContributionMembership || contribution is ContributionCommunity)
            {
                purchaseDetailsResult = await GetMembershipContributionPurchaseDetailsAsync(contribution.Id, paymentOption, couponId);
            }
            if (purchaseDetailsResult == null || purchaseDetailsResult.Failed)
            {
                _logger.LogError($"error getting purchaseDetails for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
                return OperationResult<Tuple<string, decimal>>.Failure(
                    $"error getting purchaseDetails for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
            }
            ContributionPaymentDetailsViewModel contributionPaymentDetails = (ContributionPaymentDetailsViewModel)purchaseDetailsResult.Payload;
            decimal couponDiscountInPercentage = 1m;
            if (!string.IsNullOrEmpty(couponId))
            {
                var validateCouponResult = await _couponService.ValidateByIdAsync(couponId, contribution.Id, paymentOption);
                if (validateCouponResult?.PercentAmount > 0)
                {

                    couponDiscountInPercentage = (100m - (decimal)validateCouponResult.PercentAmount) / 100;
                }
            }
            var cost = contributionPaymentDetails.DueNowWithCouponDiscountAmount / couponDiscountInPercentage;// contribution.PaymentInfo.Cost.Value;
            if (paymentOption == PaymentOptions.SessionsPackage)
            {

                if (contribution?.PaymentInfo?.PackageSessionNumbers is null)
                {
                    _logger.LogError($"PackageSession number is null for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
                    return OperationResult<Tuple<string, decimal>>.Failure(
                        $"PackageSession number is null for contribution {contribution.Id} for payment option {paymentOption.ToString()}");
                }

                cost = contribution.PaymentInfo.PackageCost.HasValue ? cost : cost * contribution.PaymentInfo.PackageSessionNumbers.Value;
            }
            if (cost > contributionPaymentDetails.DueNow) cost = contributionPaymentDetails.DueNow;
            else cost = decimal.Round(cost, 2);
            //TODO: create a tool to create prices for existed contribution and save created price id at the contribution level
            var price = await _stripeService.GetPriceForProductPaymentOptionAsync(productId, paymentOption, cost, standardAccountId) ??
                await _stripeService.CreatePriceForProductPaymentOptionAsync(productId, cost, paymentOption, contribution?.DefaultCurrency ?? "usd", standardAccountId, contribution.TaxType);


            return OperationResult<Tuple<string, decimal>>.Success(new Tuple<string,decimal>(price,cost));
        }

        private OperationResult<CreateMoneyTransferResult> HandleExistedTransfer(
            Account contributorAccount, Charge charge, Transfer existingTransfer)
        {
            var existedTransfer = new CreateMoneyTransferResult()
            {
                CoachTransfer = existingTransfer
            };

            if (contributorAccount.InvitedBy == null)
                return OperationResult<CreateMoneyTransferResult>.Success(existedTransfer);
            var affiliateUser = _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(e => e.AccountId == contributorAccount.InvitedBy).GetAwaiter().GetResult();
            var connectedStripeAccountId = affiliateUser.ConnectedStripeAccountId;
            var affiliateTransfer = _payoutService.GetTransferAsync(charge.Id, connectedStripeAccountId)
                .GetAwaiter().GetResult();

            existedTransfer.AffiliateTransfer = affiliateTransfer;

            return OperationResult<CreateMoneyTransferResult>.Success(existedTransfer);
        }

        private async Task<OperationResult> TrySubscribeWithTrialSubscription(
            ContributionBase contribution,
            User user)
        {
            var canBePaidByPaidTier = await CanBePaidByPaidTier(contribution.Id, user.AccountId);

            if (!canBePaidByPaidTier)
                return OperationResult.Failure(string.Empty);

            await _stripeService.CreateTrialSubscription(new TrialSubscriptionViewModel()
            {
                CustomerId = user.CustomerStripeAccountId,
                ContributionId = contribution.Id,
                StripeSubscriptionPlanId = contribution.PaymentInfo.MembershipInfo.ProductBillingPlans
                    .FirstOrDefault().Value.ProductBillingPlanId,
            });

            return OperationResult.Success();
        }

        private async Task<bool> CanBePaidByPaidTier(string contributionId, string accountId)
        {
            var currentPlan = await _paidTiersService.GetCurrentPaidTier(accountId);

            if (!HasActivePlan(currentPlan))
                return false;

            var bundles = await _unitOfWork.GetRepositoryAsync<BundleInfo>()
                .Get(e => e.ItemId == contributionId);

            return bundles.Any(e =>
                e.ParentId == currentPlan.PaidTierOption.Id
                || e.ParentId == currentPlan.CurrentProductPlanId);
        }

        private static bool HasActivePlan(CurrentPaidTierModel currentPlan) => currentPlan.Status == Status.Active.ToString()
            || (CancelledOrUpgraded(currentPlan) && currentPlan.EndDateTime > DateTime.UtcNow);

        private static bool CancelledOrUpgraded(CurrentPaidTierModel currentPlan) =>
            currentPlan.Status == Status.Cancel.ToString() || currentPlan.Status == Status.Upgraded.ToString();
        private async Task<OperationResult> PurchaseSessionPackageFreeWithoutCheckout(ContributionOneToOne contributionOneToOne, string userId, string couponId)
        {
            var lastPackagePurchased = contributionOneToOne.PackagePurchases?.Where(p => p.UserId == userId && p.IsConfirmed)?.LastOrDefault();
            if (lastPackagePurchased?.FreeSessionNumbers > 0)
            {
                return OperationResult.Failure("Sessions are already available in last purchase package.Try Purchase new once you booked them all.");
            }

            try
            {
                var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                             .GetOne(x => x.ContributionId == contributionOneToOne.Id && x.ClientId == userId);
                var _paymentIntent = new PurchasePayment()
                {
                    PaymentStatus = PaymentStatus.Succeeded,
                    DateTimeCharged = DateTime.UtcNow,
                    PaymentOption = PaymentOptions.FreeSessionsPackage,
                    GrossPurchaseAmount = 0,
                    TransferAmount = 0,
                    ProcessingFee = 0,
                    IsInEscrow = !contributionOneToOne.InvitationOnly,
                    PurchaseCurrency = contributionOneToOne.DefaultCurrency,
                    Currency = contributionOneToOne.DefaultCurrency,
                    TransactionId = "100_off_" + Guid.NewGuid().ToString()
                };
                //also update the purchase package list in contribution
                if (clientPurchase == null)
                {
                    clientPurchase = new Purchase()
                    {
                        ClientId = userId,
                        ContributorId = contributionOneToOne.UserId,
                        ContributionId = contributionOneToOne.Id,
                        Payments = new List<PurchasePayment>() { _paymentIntent },
                        SubscriptionId = "-2", // 100% discount subscription
                        ContributionType = contributionOneToOne.Type,
                        CouponId = couponId,
                        PaymentType = contributionOneToOne.PaymentType.ToString(),
                        TaxType = contributionOneToOne.PaymentType == PaymentTypes.Advance ? contributionOneToOne.TaxType.ToString() : string.Empty
                    };
                }
                // todo: check if we need to have a condition here before entering it
                else
                {
                    clientPurchase.Payments.Add(_paymentIntent);
                    clientPurchase.CouponId = couponId;
                }
                if (clientPurchase.Id is null)
                {
                    await _unitOfWork.GetRepositoryAsync<Purchase>().Insert(clientPurchase);
                }
                else
                {
                    _synchronizePurchaseUpdateService.Sync(clientPurchase);
                }
                //add package informtion in contribution
                contributionOneToOne.PackagePurchases.Add(new PackagePurchase
                {
                    TransactionId = _paymentIntent.TransactionId,
                    UserId = userId,
                    SessionNumbers = contributionOneToOne.PaymentInfo.PackageSessionNumbers.Value,
                    IsConfirmed = true
                });
                await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return OperationResult.Failure($"Error while joining free contribution - {ex.Message}");
            }
        }
    }
}