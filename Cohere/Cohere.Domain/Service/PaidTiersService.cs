using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Generic;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.EntitiesAuxiliary.User;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Cohere.Domain.Service
{
    public class PaidTiersService<TViewModel, TEntity> :
        GenericServiceAsync<TViewModel, TEntity>,
        IPaidTiersService<TViewModel, TEntity>
        where TViewModel : PaidTierOptionViewModel
        where TEntity : PaidTierOption
    {
        private readonly IStripeService _stripeService;
        private readonly SubscriptionService _subscriptionService;
        private readonly PlanService _planService;
        private readonly IRoleSwitchingService _roleSwitchingService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PaidTiersService<TViewModel, TEntity>> _logger;
        private readonly INotificationService _notificationService;
        private readonly StripeAccountService _stripeAccountService;

        private static Dictionary<PaidTierOptionPeriods, string> StripePlanIntervals =
            new Dictionary<PaidTierOptionPeriods, string>
            {
                { PaidTierOptionPeriods.Monthly, "month" },
                { PaidTierOptionPeriods.Annually, "year" },
            };

        private static Dictionary<PaidTierOptionPeriods, PaidTierInfo> PaidTierInfos =
            new Dictionary<PaidTierOptionPeriods, PaidTierInfo>();

        public PaidTiersService(
            IStripeService stripeService,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            SubscriptionService subscriptionService,
            PlanService planService,
            IRoleSwitchingService roleSwitchingService,
            IActiveCampaignService activeCampaignService,
            IMemoryCache memoryCache,
            ILogger<PaidTiersService<TViewModel, TEntity>> logger,
            INotificationService notificationService,
            StripeAccountService stripeAccountService)
            : base(unitOfWork, mapper)
        {
            _stripeService = stripeService;
            _subscriptionService = subscriptionService;
            _planService = planService;
            _roleSwitchingService = roleSwitchingService;
            _activeCampaignService = activeCampaignService;
            _memoryCache = memoryCache;
            _logger = logger;
            _notificationService = notificationService;
            _stripeAccountService = stripeAccountService;
        }

        public async Task<OperationResult<PaidTierOptionViewModel>> CreatePaidTierOptionProductPlan(
            TViewModel paidTierOptionVm,string contributionCurrency)
        {
            var productCreatingResult = await _stripeService.CreateProductAsync(
                new CreateProductViewModel { Id = Guid.NewGuid().ToString(), Name = paidTierOptionVm.DisplayName });

            if (productCreatingResult.Failed)
            {
                return OperationResult<PaidTierOptionViewModel>.Failure(productCreatingResult.Message);
            }

            var productId = productCreatingResult.Payload;

            StripePlanIntervals.TryGetValue(PaidTierOptionPeriods.Monthly, out var monthPeriod);

            var createMonthlyPlan = new CreateProductPlanViewModel
            {
                Name = paidTierOptionVm.DisplayName,
                ProductId = productId,
                Amount = paidTierOptionVm.PricePerMonthInCents,
                Interval = monthPeriod,
                IntervalCount = 1,
            };
            var monthlyResult = await _stripeService.CreateProductPlanAsync(createMonthlyPlan, contributionCurrency);

            if (monthlyResult.Failed)
            {
                return OperationResult<PaidTierOptionViewModel>.Failure(monthlyResult.Message);
            }

            StripePlanIntervals.TryGetValue(PaidTierOptionPeriods.Monthly, out var everySixMonth);

            var createSixMonthPlan = new CreateProductPlanViewModel
            {
                Name = paidTierOptionVm.DisplayName,
                ProductId = productId,
                Amount = paidTierOptionVm.PricePerSixMonthInCents,
                Interval = monthPeriod,
                IntervalCount = 6
                
            };
            var sixMonthlyResult = await _stripeService.CreateProductPlanAsync(createSixMonthPlan, contributionCurrency);


            if (sixMonthlyResult.Failed)
            {
                return OperationResult<PaidTierOptionViewModel>.Failure(monthlyResult.Message);
            }

            StripePlanIntervals.TryGetValue(PaidTierOptionPeriods.Annually, out var annualPeriod);

            var createAnnuallyPlan = new CreateProductPlanViewModel
            {
                Name = paidTierOptionVm.DisplayName,
                ProductId = productId,
                Amount = paidTierOptionVm.PricePerYearInCents,
                Interval = annualPeriod,
                IntervalCount = 1,
            };

            var annuallyResult = await _stripeService.CreateProductPlanAsync(createAnnuallyPlan, contributionCurrency);

            if (annuallyResult.Failed)
            {
                return OperationResult<PaidTierOptionViewModel>.Failure(annuallyResult.Message);
            }

            var paidTierInfo = new PaidTierInfo
            {
                ProductMonthlyPlanId = monthlyResult.Payload,
                ProductAnnuallyPlanId = annuallyResult.Payload,
                ProductSixMonthPlanId = sixMonthlyResult?.Payload
            };

            var paidTierOption = Mapper.Map<PaidTierOption>(paidTierOptionVm);
            paidTierOption.PaidTierInfo = paidTierInfo;

            await _unitOfWork.GetRepositoryAsync<PaidTierOption>().Insert(paidTierOption);

            return OperationResult<PaidTierOptionViewModel>.Success(null, paidTierOptionVm);
        }

        public async Task<OperationResult<string>> CreateCheckoutSessionSubscription(
            string paidTierId, PaidTierOptionPeriods period, string clientAccountId)
        {
            var currentPaidTier = await GetCurrentPaidTier(clientAccountId);
           
            if (currentPaidTier.PaidTierOption.Default)
            {
                var paidTierOption = await _unitOfWork.GetRepositoryAsync<PaidTierOption>().GetOne(p => p.Id ==
                    paidTierId);
                var client = await _unitOfWork.GetRepositoryAsync<User>()
                    .GetOne(u => u.AccountId == clientAccountId);

                Cohere.Entity.Entities.Account account = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Id == clientAccountId);
                Cohere.Entity.Entities.Account coachAccount = null;
                User coachUser = null;

                if (!string.IsNullOrWhiteSpace(account.InvitedBy))
                {
                    coachAccount = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Id == account.InvitedBy);
                    coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == coachAccount.Id);
                    if (coachUser.IsBetaUser == false)
                        coachUser = null;
                }

                //update coach customerId according to plateform 
                var customerResult = _stripeAccountService.GetCustomerAccountList(account.Email);
                var customerList = (List<StripeCustomerAccount>)customerResult.Payload;
                if (customerList == null || customerList.Count == 0)
                {
                    return OperationResult<string>.Failure("No such customer exist");
                }

                var customerStripeAccountId = customerList.FirstOrDefault(c => c.Currency == "usd" || string.IsNullOrEmpty(c.Currency)).CustomerId;
                if (customerStripeAccountId != client.CustomerStripeAccountId)
                {
                    client.CustomerStripeAccountId = customerStripeAccountId;
                    await _unitOfWork.GetRepositoryAsync<User>().Update(client.Id, client);
                }

                string priceId = String.Empty;
                switch(period) 
                {
                    case PaidTierOptionPeriods.EverySixMonth:
                    {
                        priceId = paidTierOption.PaidTierInfo.ProductSixMonthPlanId;
                        break;
                    }
                    case PaidTierOptionPeriods.Annually:
                    {
                        priceId = paidTierOption.PaidTierInfo.ProductAnnuallyPlanId;
                        break;
                    }
                    default:
                    {
                        priceId = paidTierOption.PaidTierInfo.ProductMonthlyPlanId;
                        break;
                    }
                }

                ClearCache("currentPaidTier_" + clientAccountId);

                return await _stripeService.CreateCheckoutSessionSubscription(customerStripeAccountId, priceId, paidTierOption, coachUser?.ConnectedStripeAccountId);
            }

            return new OperationResult<string>(false, "You cannot buy new paid tier since you already have one. Try upgrade or cancel.");
        }

        public async Task<CurrentPaidTierViewModel> GetCurrentPaidTierViewModel(string accountId)
        {
            var model = await GetCurrentPaidTier(accountId);

            CurrentPaidTierViewModel mappedModel = new CurrentPaidTierViewModel
            {
                PaidTierOption = Mapper.Map<PaidTierOptionViewModel>(model.PaidTierOption),
                NextPaidTierOption = Mapper.Map<PaidTierOptionViewModel>(model.NextPaidTierOption),
                EndDateTime = model.EndDateTime,
                Status = model.Status,
                CurrentPaymentPeriod = model.CurrentPaymentPeriod,
                NextPaymentPeriod = model.NextPaymentPeriod
            };

            return mappedModel;
        }

        public async Task<OperationResult> CancelPaidTierPlan(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var paidTierPurchase = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(p => p.ClientId == user.Id)).OrderByDescending(p => p.CreateTime).FirstOrDefault();

            if (paidTierPurchase is null)
            {
                return OperationResult.Failure("Paid tier plan is not purchased yet");
            }

            var cancelPaidTierPlanResult =
                await _stripeService.CancelSubscriptionAtPeriodEndAsync(paidTierPurchase.SubscriptionId);

            if (cancelPaidTierPlanResult.Succeeded)
            {
                var activeCampaignDeal = new ActiveCampaignDeal()
                {
                    Value = "0"
                };
                string paidTearOption = new CohereDealCustomFieldPaidTear().AccountCanceled;
                ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                {
                    CohereAccountId = accountId,
                    PaidTier = paidTearOption,
                    AccountCancelDate = DateTime.UtcNow.ToString("MM/dd/yyyy"),
                };
                _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);

                var subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(paidTierPurchase.SubscriptionId);
                var subscription = subscriptionResult.Payload;
                var currentPaidtierPlan = await GetPaidTierByPlanId(subscription.Plan.Id);
                var billingFrequency = currentPaidtierPlan.PaidTierInfo.GetStatus(subscription.Plan.Id);
                var planName = currentPaidtierPlan.DisplayName;
                var endOfMembershipDate = paidTierPurchase.Payments.Last().PeriodEnds;
                var cancellationDate = subscription.CanceledAt;
                await _notificationService.SendCohrealerPaidTierAccountCancellationNotificationToAdmins(accountId, user, planName, billingFrequency.ToString(), endOfMembershipDate, cancellationDate);
            }

            ClearCache("currentPaidTier_" + accountId);

            return cancelPaidTierPlanResult.Failed ? cancelPaidTierPlanResult : OperationResult.Success();
        }

        public async Task<OperationResult> UpgradePaidTierPlan(string accountId, string desiredPaidTierId, PaidTierOptionPeriods newPaymentPeriod)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var allPurchasedPlans = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(pt => pt.ClientId == user.Id)).ToList();

            var currentPurchasePlan = allPurchasedPlans.OrderByDescending(p => p.CreateTime).FirstOrDefault();

            if (currentPurchasePlan is null)
            {
                return OperationResult.Failure("Paid tier plan is not purchased yet");
            }

            var subscription = await _subscriptionService.GetAsync(currentPurchasePlan.SubscriptionId);

            if (subscription.Status != "active")
            {
                return OperationResult.Failure("There is no active plans now");
            }

            if (!string.IsNullOrEmpty(subscription.ScheduleId))
            {
                return OperationResult.Failure("another upgrade in progress");
            }

            if (subscription.CancelAtPeriodEnd)
            {
                return OperationResult.Failure("cancel in progress");
            }

            var currentPaidTier = await GetPaidTierByPlanId(subscription.Plan.Id);
            var currentPaymentOption = PaidTierOptionPeriods.Monthly;

            if(subscription.Plan.Id == currentPaidTier.PaidTierInfo.ProductAnnuallyPlanId)
                currentPaymentOption = PaidTierOptionPeriods.Annually;
            else if (subscription.Plan.Id == currentPaidTier.PaidTierInfo.ProductMonthlyPlanId)
                currentPaymentOption = PaidTierOptionPeriods.Monthly;
            else if (subscription.Plan.Id == currentPaidTier.PaidTierInfo.ProductSixMonthPlanId)
                currentPaymentOption = PaidTierOptionPeriods.EverySixMonth;


            if (newPaymentPeriod == currentPaymentOption && currentPaidTier.Id == desiredPaidTierId)
            {
                return OperationResult.Failure("The chosen payment period and plan are already active");
            }

            var desiredPaidTier = await _unitOfWork.GetRepositoryAsync<PaidTierOption>()
                .GetOne(e => e.Id == desiredPaidTierId);

            if (desiredPaidTier.Default)
            {
                return OperationResult.Failure("Can not upgrade to default Paid Tier, use cancel instead");
            }

            var desiredPlanId = desiredPaidTier.PaidTierInfo.ProductMonthlyPlanId;

            if (newPaymentPeriod == PaidTierOptionPeriods.Monthly)
                desiredPlanId = desiredPaidTier.PaidTierInfo.ProductMonthlyPlanId;
            else if (newPaymentPeriod == PaidTierOptionPeriods.Annually)
                desiredPlanId = desiredPaidTier.PaidTierInfo.ProductAnnuallyPlanId;
            else if (newPaymentPeriod == PaidTierOptionPeriods.EverySixMonth)
                desiredPlanId = desiredPaidTier.PaidTierInfo.ProductSixMonthPlanId;

            var upgradeSubscriptionResult = await _stripeService
                .UpgradePaidTierPlanAsync(currentPurchasePlan.SubscriptionId, desiredPlanId);

            if (upgradeSubscriptionResult.Succeeded)
            {
                var newPlan = await _planService.GetAsync(desiredPlanId);
                var activeCampaignDeal = new ActiveCampaignDeal()
                {
                    Value = newPlan?.AmountDecimal?.ToString()
                };
                string paidTearOption = _activeCampaignService.PaidTearOptionToActiveCampaignDealCustomFieldValue(desiredPaidTier, newPaymentPeriod);
                ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                {
                    CohereAccountId = accountId,
                    PaidTier = paidTearOption

                };
                _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
            }

            ClearCache("currentPaidTier_" + accountId);

            return upgradeSubscriptionResult.Failed ? upgradeSubscriptionResult : OperationResult.Success();
        }

        private void ClearCache(string key)
		{
            _memoryCache.Remove(key);
		}

        public async Task<CurrentPaidTierModel> GetCurrentPaidTier(string accountId, DateTime? atDateTime = null)
        {
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var allPurchasedPlans = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(pt => pt.ClientId == client.Id)).ToList();

            var currentPurchasePlan = allPurchasedPlans.OrderByDescending(p => p.CreateTime).FirstOrDefault();

            if (currentPurchasePlan is null)
            {
                return await GetDefaultPaidTierViewModel();
            }

            var subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(currentPurchasePlan.SubscriptionId);

            var subscription = subscriptionResult.Payload;

            if ((atDateTime == null && (subscription?.Status != "active" && subscription?.Status != "trialing")) ||
                (subscription == null) ||
                ((atDateTime != null && (subscription?.StartDate > atDateTime?.Date) ||
                subscription.Status == "canceled" && subscription.EndedAt < atDateTime)))
            {
                return await GetDefaultPaidTierViewModel();
            }

            var currentPaidTier = await GetPaidTierByPlanId(subscription.Plan.Id);
            if(currentPaidTier == null)
            {
                return await GetDefaultPaidTierViewModel();
            }
            var currentPaymentPeriod = currentPaidTier.PaidTierInfo.GetStatus(subscription.Plan.Id);

            if (subscription.CancelAtPeriodEnd || subscription.Status == "canceled")
            {
                var defaultPaidTier = await GetDefaultPaidTierViewModel();
                return new CurrentPaidTierModel
                {
                    Status = Status.Cancel.ToString(),
                    PaidTierOption = currentPaidTier,
                    CurrentPaymentPeriod = currentPaymentPeriod.ToString(),
                    NextPaidTierOption = defaultPaidTier.PaidTierOption,
                    StartDateTime = subscription.StartDate,
                    EndDateTime = subscription.CancelAt,
                    CanceledAt = subscription.CanceledAt,
                    EndedAtDateTime = subscription.EndedAt,
                    CurrentProductPlanId = subscription.Plan.Id,
                    Version = currentPaidTier.Version
                };
            }

            if (!string.IsNullOrEmpty(subscription.ScheduleId))
            {
                var upgradedPlanId = GetNextPhasesPlanId(subscription);
                var upgradedPaidTier = await GetPaidTierByPlanId(upgradedPlanId);
                var upgradedPaymentPeriod = currentPaidTier.PaidTierInfo.GetStatus(upgradedPlanId);

                return new CurrentPaidTierModel
                {
                    Status = Status.Upgraded.ToString(),
                    PaidTierOption = currentPaidTier,
                    CurrentPaymentPeriod = currentPaymentPeriod.ToString(),
                    StartDateTime = subscription.StartDate,
                    EndDateTime = subscription.CancelAt,
                    EndedAtDateTime = subscription.EndedAt,
                    NextPaidTierOption = upgradedPaidTier,
                    NextPaymentPeriod = upgradedPaymentPeriod.ToString(),
                    CurrentProductPlanId = subscription.Plan.Id,
                    Version = currentPaidTier.Version
                };
            }

            return new CurrentPaidTierModel
            {
                Status = Status.Active.ToString(),
                PaidTierOption = currentPaidTier,
                CurrentPaymentPeriod = currentPaymentPeriod.ToString(),
                StartDateTime = subscription.StartDate,
                EndDateTime = subscription.CancelAt ?? subscription.CurrentPeriodEnd,
                EndedAtDateTime = subscription.EndedAt,
                CurrentProductPlanId = subscription.Plan.Id,
                Version = currentPaidTier.Version
            };
        }

        public override Task<OperationResult> Delete(string id)
        {
            throw new NotImplementedException();
        }

        public override Task<OperationResult> Update(TViewModel view)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<TViewModel>> GetAll(string accountId)
        {
            var currentPaidTier = await GetCurrentPaidTier(accountId);

            var entities = await _unitOfWork.GetRepositoryAsync<PaidTierOption>().Get(x=>x.Version == currentPaidTier.Version);

            return Mapper.Map<IEnumerable<TViewModel>>(entities);
        }

        private async Task<CurrentPaidTierModel> GetDefaultPaidTierViewModel()
        {
            var plans = await _unitOfWork.GetRepositoryAsync<PaidTierOption>().GetAll();
            var currentVersion = plans.Max(x => x.Version);
            var defaultPaidTier = await _unitOfWork.GetRepositoryAsync<PaidTierOption>().GetOne(e => e.Default && e.Version == currentVersion);
            return new CurrentPaidTierModel
            {
                PaidTierOption = defaultPaidTier,
                Version = currentVersion
            };
        }

        private string GetNextPhasesPlanId(Subscription subscription)
        {
            var nextPhasePlanId = subscription
                .Schedule
                .Phases.LastOrDefault()
                ?.Plans.FirstOrDefault()
                ?.PlanId;

            if (!string.IsNullOrEmpty(nextPhasePlanId))
            {
                return nextPhasePlanId;
            }

            _logger.LogError("next phase plan Id is null");
            throw new Exception("next phase plan Id is null");
        }

        private async Task<PaidTierOption> GetPaidTierByPlanId(string planId)
        {
            return await _unitOfWork.GetRepositoryAsync<PaidTierOption>()
                .GetOne(p =>
                    p.PaidTierInfo.ProductMonthlyPlanId == planId
                    || p.PaidTierInfo.ProductAnnuallyPlanId == planId
                    || p.PaidTierInfo.ProductSixMonthPlanId == planId);
             
        }
    }
}