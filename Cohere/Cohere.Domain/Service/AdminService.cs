using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
using AutoMapper;

using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.AdminViewModels;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using static Cohere.Domain.Utils.Constants;

namespace Cohere.Domain.Service
{
    //
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;
        private readonly ICouponService _couponService;
        private readonly IStripeService _stripeService;
        private readonly IPricingCalculationService _pricingCalculationService;
        private readonly ICohealerIncomeService _cohealerIncomeService;
        private readonly Stripe.BalanceTransactionService _balanceTransactionService;
        private readonly IMemoryCache _memoryCache;
        private readonly IMapper _mapper;
        private readonly ILogger<ContributionPurchaseService> _logger;

        public AdminService(IUnitOfWork unitOfWork, IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService,
            ICouponService couponService,
            IStripeService stripeService,
            IPricingCalculationService pricingCalculationService,
            ICohealerIncomeService cohealerIncomeService,
            Stripe.BalanceTransactionService balanceTransactionService,
            IMemoryCache memoryCache,
            IMapper mapper,
            ILogger<ContributionPurchaseService> logger)
        {
            _unitOfWork = unitOfWork;
            _paidTiersService = paidTiersService;
            _couponService = couponService;
            _stripeService = stripeService;
            _pricingCalculationService = pricingCalculationService;
            _cohealerIncomeService = cohealerIncomeService;
            _balanceTransactionService = balanceTransactionService;
            _memoryCache = memoryCache;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<OperationResult<KpiReportResultViewModel>>
            GetKpiReportAsync(KpiReportRequestViewModel viewModel)
        {
            KpiReportResultViewModel result = new KpiReportResultViewModel();

            var allUsers = await _unitOfWork.GetGenericRepositoryAsync<User>().Collection
                .Aggregate()
                .Match(u => !string.IsNullOrEmpty(u.ConnectedStripeAccountId))
                .Lookup<User, PaidTierPurchase, User>(_unitOfWork.GetGenericRepositoryAsync<PaidTierPurchase>().Collection, user => user.Id,
                        paidTierPurchase => paidTierPurchase.ClientId, result => result.PaidTierPurchases)
                .Lookup<User, ContributionBase, User>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection, user => user.Id,
                        contribution => contribution.UserId, result => result.Contributions)
                .Lookup<User, Purchase, User>(_unitOfWork.GetGenericRepositoryAsync<Purchase>().Collection, user => user.Id,
                        purchase => purchase.ClientId, result => result.Purchases)
                .Lookup<User, Account, User>(_unitOfWork.GetGenericRepositoryAsync<Account>().Collection, user => user.AccountId,
                        account => account.Id, result => result.Account)
                .Lookup<User, UserActivity, User>(_unitOfWork.GetGenericRepositoryAsync<UserActivity>().Collection, user => user.Id,
                        userActivity => userActivity.UserId, result => result.UserActivities)
                .Unwind<User, User>(user => user.Account)
                .ToListAsync();

            ConcurrentDictionary<string, Stripe.Subscription> userPaidTierStatus = new ConcurrentDictionary<string, Stripe.Subscription>();
            allUsers = allUsers.Where(u => u.CreateTime.Date <= viewModel.To.Date)?.ToList();
            var newUsers = allUsers.Where(u => u.CreateTime.Date >= viewModel.From.Date).ToList();

            List<List<User>> userChunkList = new List<List<User>>();
            var totalUserCount = allUsers.Count();
            var NoOfTasks = 10;
            var sizeOfChunk =(int)Math.Ceiling(totalUserCount/(double)NoOfTasks);
            var skipCount = 0;
            while (skipCount < totalUserCount)
            {
                userChunkList.Add(allUsers.Skip(skipCount).Take(sizeOfChunk).ToList());
                skipCount += sizeOfChunk;
            }

            var tasks = new List<Task>();
            foreach  (var  userList in userChunkList) 
            {   
               // tasks.Add(Task.Run(async () =>
                //{
                    foreach (var user in userList)
                    {
                        try
                        {
                            if (user.PaidTierPurchases?.Count() > 0)
                            {
                                var allPurchasedPlans = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                                    .Get(pt => pt.ClientId == user.Id)).ToList();

                                var currentPurchasePlan = allPurchasedPlans.OrderByDescending(p => p.CreateTime).FirstOrDefault();

                                if (currentPurchasePlan != null && currentPurchasePlan.Payments?.Count > 0)
                                {

                                    int payments = currentPurchasePlan.Payments.Count;
                                    //var subscriptionResult = await _stripeService.GetProductPlanSubscriptionAsync(currentPurchasePlan.SubscriptionId);
                                    userPaidTierStatus.TryAdd(user.Id, 
                                        new Stripe.Subscription 
                                        { 
                                            CurrentPeriodStart = currentPurchasePlan.Payments[payments - 1].DateTimeCharged, 
                                            StartDate = currentPurchasePlan.Payments[payments - 1].DateTimeCharged, 
                                            EndedAt = currentPurchasePlan.Payments[payments - 1].PeriodEnds.Value,
                                            CurrentPeriodEnd = currentPurchasePlan.Payments[payments - 1].PeriodEnds.Value,
                                            CancelAt = currentPurchasePlan.Payments[payments -1].PeriodEnds.Value.AddDays(1)                                        
                                        });
                                }
                                else
                                {
                                    userPaidTierStatus.TryAdd(user.Id, null);
                                }
                            }
                            else
                            {
                                userPaidTierStatus.TryAdd(user.Id, null);
                            }
                        }

                        catch
                        {
                            _logger.LogError($"Error caused by {user.Id} while fetching Kpi report");
                        }
                    }
                //}));               
            }

           //Task.WhenAll(tasks).Wait();

            var lunchPlanUsers = allUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) &&
                (
                userPaidTierStatus[u.Id] == null ||
                userPaidTierStatus[u.Id]?.CurrentPeriodStart > viewModel.To.Date ||
                userPaidTierStatus[u.Id]?.CurrentPeriodEnd < viewModel.From.Date
                )

            );
            var lunchPlanNewUsers = newUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) &&
                (
                userPaidTierStatus[u.Id] == null ||
                userPaidTierStatus[u.Id]?.StartDate >= viewModel.To.Date ||
                userPaidTierStatus[u.Id]?.EndedAt <= viewModel.From.Date
                ) 
            );
            var paidTierPlanUsers = allUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) &&
                (
                userPaidTierStatus[u.Id]?.CurrentPeriodStart <= viewModel.To.Date &&
                userPaidTierStatus[u.Id]?.CurrentPeriodEnd >= viewModel.To.Date
                ) 

            );
            var paidTierPlanNewUsers = newUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) &&
                (
                userPaidTierStatus[u.Id]?.CurrentPeriodStart >= viewModel.From.Date 
                ) 
            );
            var canceledPaidTierUsers = allUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) && 
                u.CreateTime.Date <= viewModel.To.Date &&
                    (
                    userPaidTierStatus[u.Id]?.CurrentPeriodEnd <= viewModel.From.Date ||
                    userPaidTierStatus[u.Id]?.CancelAt <= viewModel.From.Date
                    )
            );
            var canceledPaidTierNewUsers = newUsers.Where(u =>
                userPaidTierStatus.ContainsKey(u.Id) &&
                (
                userPaidTierStatus[u.Id]?.CurrentPeriodEnd <= viewModel.From.Date ||
                userPaidTierStatus[u.Id]?.CancelAt <= viewModel.From.Date
                )
            );

            result.NumberOfTotalLaunchPlanUsersWithAccountAndNoContributions = lunchPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfNewLaunchPlanUsersWithAccountAndNoContributions = lunchPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfTotalLaunchPlanUsersWithContributionAndNoSales = lunchPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfNewLaunchPlanUsersWithContributionAndNoSales = lunchPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date >= viewModel.From.Date && p.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfTotalLaunchPlanUsersMadeSales = lunchPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();
            result.NumberOfNewLaunchPlanUsersMadeSales = lunchPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date >= viewModel.From.Date && p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();

            result.NumberOfTotalPaidTierPlansUsersWithAccountAndNoContributions = paidTierPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfNewPaidTierPlansUsersWithAccountAndNoContributions = paidTierPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfTotalPaidTierPlansUsersWithContributionAndNoSales = paidTierPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfNewPaidTierPlansUsersWithContributionAndNoSales = paidTierPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date >= viewModel.From.Date && p.CreateTime.Date <= viewModel.To.Date).Count() == 0).Count();
            result.NumberOfTotalPaidTierPlansUsersMadeSales = paidTierPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();
            result.NumberOfNewPaidTierPlansUsersMadeSales = paidTierPlanNewUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date >= viewModel.From.Date && c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date >= viewModel.From.Date && p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();

            result.NumberOfNewPaidTiers = paidTierPlanNewUsers.Count();
            result.NumberOfTotalPaidTiers = paidTierPlanUsers.Count();
            result.NumberOfLaunchPlanMembersMadeSalesDuringReportTime = lunchPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date && p.CreateTime.Date >= viewModel.From.Date).Count() > 0).Count();
            result.NumberOfPaidTierPlansUsersMadeSalesDuringReportTime = paidTierPlanUsers.Where(u => u.Contributions.Where(c => c.CreateTime.Date <= viewModel.To.Date).Count() > 0 &&
                u.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date && p.CreateTime.Date >= viewModel.From.Date).Count() > 0).Count();
            result.NumberOfNewAccounts = allUsers.Where(u => u.CreateTime.Date >= viewModel.From.Date && u.CreateTime.Date <= viewModel.From.Date).Count();

            var allReferalsUsers = allUsers.Where(u => !string.IsNullOrEmpty(u.Account?.InvitedBy));

            result.TotalNumberOfReferrals = allReferalsUsers.Where(r => r.CreateTime.Date <= viewModel.To.Date).Count();
            result.TotalNumberOfNewReferrals = allReferalsUsers.Where(r => r.CreateTime.Date <= viewModel.To.Date && r.CreateTime.Date >= viewModel.From.Date).Count();
            result.TotalNumberOfReferralsWithSales = allReferalsUsers.Where(r => r.CreateTime.Date <= viewModel.To.Date &&
                r.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();
            result.TotalNumberOfNewReferralsWithSales = allReferalsUsers.Where(r => r.CreateTime.Date <= viewModel.To.Date && r.CreateTime.Date >= viewModel.From.Date &&
                r.Purchases.Where(p => p.CreateTime.Date <= viewModel.To.Date).Count() > 0).Count();

            result.NumberOfNewActivePaidTierCoaches = paidTierPlanNewUsers
                .Where(u => u.UserActivities?
                .Where(a => a.ActivityTimeUTC >= viewModel.From.Date &&
                        a.ActivityTimeUTC <= viewModel.To)?.Count() > 0)?.Count() ?? 0;
            result.NumberOfTotalActivePaidTierCoaches = paidTierPlanUsers
                .Where(u => u.UserActivities?
                .Where(a => a.ActivityTimeUTC <= viewModel.To)?.Count() > 0)?.Count() ?? 0;
            result.NumberOfNewActiveFreeTierCoaches = lunchPlanNewUsers
                .Where(u => u.UserActivities?
                .Where(a => a.ActivityTimeUTC >= viewModel.From &&
                        a.ActivityTimeUTC <= viewModel.To)?.Count() > 0)?.Count() ?? 0;
            result.NumberOfTotalActiveFreeTierCoaches = lunchPlanUsers
                .Where(u => u.UserActivities?
                .Where(a => a.ActivityTimeUTC <= viewModel.To)?.Count() > 0)?.Count() ?? 0;

            result.NumberOfNewCanceledPaidTiers = canceledPaidTierNewUsers.Count();
            result.NumberOfTotalCanceledPaidTiers = canceledPaidTierUsers.Count();

            return OperationResult<KpiReportResultViewModel>.Success(result);
        }

        public async Task<OperationResult<ActiveCampaignReportResultViewModel>> GetActiveCampaignReportAsync()
        {
            ActiveCampaignReportResultViewModel result = new ActiveCampaignReportResultViewModel();

            var allUsers = await _unitOfWork.GetGenericRepositoryAsync<User>().Collection
                    .Aggregate()
                    .Match(u => !string.IsNullOrEmpty(u.ConnectedStripeAccountId))
                    .Lookup<User, PaidTierPurchase, User>(_unitOfWork.GetGenericRepositoryAsync<PaidTierPurchase>().Collection, user => user.Id,
                            paidTierPurchase => paidTierPurchase.ClientId, result => result.PaidTierPurchases)
                    .Lookup<User, ContributionBase, User>(_unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Collection, user => user.Id,
                            contribution => contribution.UserId, result => result.Contributions)
                    .Lookup<User, Purchase, User>(_unitOfWork.GetGenericRepositoryAsync<Purchase>().Collection, user => user.Id,
                            purchase => purchase.ContributorId, result => result.Purchases)
                    .ToListAsync();

            var allAccounts = await _unitOfWork.GetGenericRepositoryAsync<Account>().GetAll();
            foreach (var user in allUsers)
            {
                ActiveCampaignReportItemViewModel item = new ActiveCampaignReportItemViewModel();
                var userAccount = allAccounts?.FirstOrDefault(a => a.Id == user.AccountId);

                item.Email = userAccount?.Email;
                item.FirstName = user.FirstName;
                item.LastName = user.LastName;
                item.AccountCreatedDate = user.CreateTime.ToString("MM/dd/yyyy");
                item.PaidTier = new CohereDealCustomFieldPaidTear().Launch;

                var latestPaidTierPurchase = user.PaidTierPurchases?.Where(p =>
                    new List<PaymentStatus?>() { PaymentStatus.Succeeded, PaymentStatus.Paid }.Contains(p.Payments?.OrderByDescending(p => p.DateTimeCharged)?.FirstOrDefault()?.PaymentStatus))?
                    .OrderByDescending(p => p.CreateTime)?
                    .FirstOrDefault();
                if (latestPaidTierPurchase != null)
                {
                    try
                    {
                        var paidTier = await _paidTiersService.GetCurrentPaidTier(user.AccountId);
                        if (!string.IsNullOrEmpty(paidTier?.PaidTierOption?.DisplayName))
                        {
                            if (paidTier.PaidTierOption.DisplayName == PaidTierTitles.Impact)
                            {
                                if (paidTier.CurrentPaymentPeriod == "Monthly")
                                {
                                    item.PaidTier = new CohereDealCustomFieldPaidTear().ImpactMonthly;
                                }
                                else if (paidTier.CurrentPaymentPeriod == "Annually")
                                {
                                    item.PaidTier = new CohereDealCustomFieldPaidTear().ImpactAnnual;
                                }
                                else
                                {
                                    item.PaidTier = new CohereDealCustomFieldPaidTear().ImpactSixMonth;
                                }
                            }
                            if (paidTier.PaidTierOption.DisplayName == PaidTierTitles.Scale)
                            {
                                if (paidTier.CurrentPaymentPeriod == "Monthly")
                                {
                                    item.PaidTier = new CohereDealCustomFieldPaidTear().ScaleAnnual;
                                }
                                else if (paidTier.CurrentPaymentPeriod == "Annually")
                                {
                                    item.PaidTier = new CohereDealCustomFieldPaidTear().ScaleMonthly;
                                }
                            }
                        }
                        item.CanceledDate = paidTier.CanceledAt?.ToString("MM/dd/yyyy");
                    }
                    catch
                    {

                    }
                }

                item.NumberOfContributions = user.Contributions?.Count() ?? 0;
                item.ContributionCratedDates = string.Join(';', user.Contributions?.Select(c => c.CreateTime.ToString("MM/dd/yyyy")) ?? new List<string>());
                item.FirstContributionCratedDate = user.Contributions?.OrderBy(c => c.CreateTime)?.FirstOrDefault()?.CreateTime.ToString("MM/dd/yyyy");

                // get purchase date related data
                List<DateTime> purchasesDates = user.Purchases?.SelectMany(c => c.Payments)?
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
                string acHasAchieved3ConsecutiveMonths = EnumHelper<HasAchieved3ConsecutiveMonthsOfRevenue>.GetDisplayValue(
                    consecutiveMonths >= 3 ? HasAchieved3ConsecutiveMonthsOfRevenue.Yes : HasAchieved3ConsecutiveMonthsOfRevenue.No);
                string acRevenue = EnumHelper<Revenue>.GetDisplayValue(purchasesDates?.Count == 0 ? Revenue.PreRevenue :
                    (purchasesDates?.Count > 1 ? Revenue.MonthlyRevenue : Revenue.Revenue));

                item.RevenuStatus = acRevenue;
                item.HasAchieved2MonthsOfRevenue = acHasAchieved2Months;
                item.HasAchieved3ConsecutiveMonthsOfRevenue = acHasAchieved3ConsecutiveMonths;

                List<DateTime> updateDates = new List<DateTime>();
                if (user.Contributions?.Count() > 0)
                {
                    updateDates.AddRange(user.Contributions.Select(c => c.UpdateTime));
                }

                updateDates.Add(user.UpdateTime);
                updateDates.Add(user.CreateTime);
                updateDates = updateDates.Where(d => d != default(DateTime) && d != DateTime.MinValue).ToList();
                updateDates = updateDates.OrderByDescending(d => d).ToList();

                item.LastCohereActivityDate = updateDates.FirstOrDefault().ToString("MM/dd/yyyy");

                result.ActiveCampaignReportItems.Add(item);
            }
            return OperationResult<ActiveCampaignReportResultViewModel>.Success(result);
        }

        public async Task<OperationResult<IEnumerable<PurchasesWithCouponCodeViiewModel>>> GetPurchasesWithCouponCode()
        {
            List<PurchasesWithCouponCodeViiewModel> result = new List<PurchasesWithCouponCodeViiewModel>();
            var allPurchasesWithCoupons = await _unitOfWork.GetRepositoryAsync<Purchase>()
            .Get(p => !string.IsNullOrEmpty(p.CouponId));
            allPurchasesWithCoupons = allPurchasesWithCoupons.OrderByDescending(p => p.CreateTime);
            foreach (var purchase in allPurchasesWithCoupons)
            {
                var user = await _unitOfWork.GetRepositoryAsync<User>()
                    .GetOne(u => u.Id == purchase.ClientId);
                if (user != null)
                {
                    var account = await _unitOfWork.GetRepositoryAsync<Account>()
                    .GetOne(a => a.Id == user.AccountId);
                    if (account != null)
                    {
                        result.Add(new PurchasesWithCouponCodeViiewModel()
                        {
                            ClientEmail = account.Email,
                            CouponId = purchase.CouponId,
                            DateOfPurchase = purchase.CreateTime,
                            PurchaseId = purchase.Id
                        });
                    }
                }
            }
            return OperationResult<IEnumerable<PurchasesWithCouponCodeViiewModel>>.Success(result);
        }

        public async Task<OperationResult<IEnumerable<Purchase>>> UpdateAllClientPurchasesWithStripeData(bool previewOnly)
        {
            var allPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetAll();

            var purchasesToReturn = new List<Purchase>();

            if (allPurchases.Any())
            {
                foreach (var purchase in allPurchases)
                {
                    if (!string.IsNullOrEmpty(purchase?.ContributionId))
                    {
                        var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == purchase.ContributionId);
                        if (contribution != null)
                        {
                            if (purchase.Payments.Any())
                            {
                                int paymentIndex = 0;
                                int paymentCount = purchase.Payments.Count;
                                foreach (var payment in purchase.Payments)
                                {
                                    paymentIndex++;
                                    string invoiceId = payment.InvoiceId;
                                    string transactionId = payment.TransactionId;
                                    if (!payment.IsTrial)
                                    {
                                        if (!string.IsNullOrEmpty(invoiceId))
                                        {
                                            var invoice = await _stripeService.GetInvoiceAsync(invoiceId);
                                            if (invoice != null)
                                            {
                                                if (payment?.TransferAmount > 0 || invoice?.TransferData?.Amount > 0)
                                                {
                                                    if (invoice?.Charge != null)
                                                    {
                                                        Stripe.BalanceTransaction balanceTransaction = null;
                                                        var charge = invoice.Charge;
                                                        if (charge.BalanceTransaction == null)
                                                        {
                                                            balanceTransaction = _balanceTransactionService.Get(charge.BalanceTransactionId);
                                                        }
                                                        else
                                                        {
                                                            balanceTransaction = charge.BalanceTransaction;
                                                        }

                                                        if (balanceTransaction != null)
                                                        {
                                                            var ammount = balanceTransaction.Amount / _stripeService.SmallestCurrencyUnit;
                                                            var transferAmount = payment.TransferAmount;
                                                            if (invoice?.TransferData?.Amount > 0)
                                                            {
                                                                transferAmount = (decimal)(invoice.TransferData.Amount / _stripeService.SmallestCurrencyUnit);
                                                            }
                                                            var processingFee = balanceTransaction.Fee / _stripeService.SmallestCurrencyUnit;
                                                            if (ammount > 0 && transferAmount > 0 && processingFee > 0)
                                                            {
                                                                await UpdatePurchasePayment(purchase, payment, contribution, ammount, (decimal)transferAmount, (decimal)processingFee);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else if (!string.IsNullOrEmpty(transactionId) &&
                                            transactionId.ToLower().StartsWith("pi_"))
                                        {
                                            var paymentIntent = await _stripeService.GetPaymentIntentAsync(transactionId);
                                            if (paymentIntent != null)
                                            {
                                                if (payment?.TransferAmount > 0 || paymentIntent?.TransferData?.Amount > 0)
                                                {
                                                    if (paymentIntent?.Charges?.Count() > 0)
                                                    {
                                                        Stripe.BalanceTransaction balanceTransaction = null;
                                                        var lastCharge = paymentIntent?.Charges?.Data?.LastOrDefault(x => x.Status == "succeeded");
                                                        if (lastCharge != null)
                                                        {
                                                            if (lastCharge?.BalanceTransaction == null)
                                                            {
                                                                balanceTransaction = _balanceTransactionService.Get(lastCharge?.BalanceTransactionId);
                                                            }
                                                            else
                                                            {
                                                                balanceTransaction = lastCharge.BalanceTransaction;
                                                            }

                                                            if (balanceTransaction != null)
                                                            {
                                                                var ammount = balanceTransaction.Amount / _stripeService.SmallestCurrencyUnit;
                                                                var transferAmount = payment.TransferAmount;
                                                                if (paymentIntent?.TransferData?.Amount > 0)
                                                                {
                                                                    transferAmount = (decimal)(paymentIntent.TransferData.Amount / _stripeService.SmallestCurrencyUnit);
                                                                }
                                                                var processingFee = balanceTransaction.Fee / _stripeService.SmallestCurrencyUnit;
                                                                if (ammount > 0 && transferAmount > 0 && processingFee > 0)
                                                                {
                                                                    await UpdatePurchasePayment(purchase, payment, contribution, ammount, transferAmount, processingFee);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (paymentIndex == paymentCount)
                                        {
                                            if (!previewOnly)
                                            {
                                                await _unitOfWork.GetRepositoryAsync<Purchase>().Update(purchase.Id, purchase);
                                            }
                                            purchasesToReturn.Add(purchase);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return OperationResult<IEnumerable<Purchase>>.Success(purchasesToReturn);
            }

            return OperationResult<IEnumerable<Purchase>>.Failure("Updating client purchases with stripe data failed");
        }

        private async Task UpdatePurchasePayment(Purchase clientPurchase, PurchasePayment payment,
            ContributionBase contribution,
            decimal amount, decimal transferAmount, decimal processingFee)
        {
            bool coachPaysStripeFee = contribution.PaymentInfo.CoachPaysStripeFee;
            var contributionOwner =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(contributionOwner.AccountId, payment.DateTimeCharged);
            var serviceProviderIncome = _pricingCalculationService.CalculateServiceProviderIncome(
                amount,
                coachPaysStripeFee,
                currentPaidTier.PaidTierOption.NormalizedFee,
                contribution.PaymentType,
                contributionOwner.CountryId,
                processingFee);


            payment.TransferAmount = transferAmount;
            payment.ProcessingFee = processingFee;
            payment.CohereFee = serviceProviderIncome.PlatformFee;
            decimal coachFee = amount - payment.TransferAmount - payment.CohereFee;
            if (!coachPaysStripeFee)
            {
                coachFee -= payment.ProcessingFee;
            }
            if (coachFee >= 0)
            {
                payment.CoachFee = coachFee;
            }
            decimal clientFee = coachPaysStripeFee ? 0 :
                amount - payment.TransferAmount - payment.CoachFee - payment.CohereFee;
            if (clientFee >= 0)
            {
                payment.ClientFee = clientFee;
            }
            payment.GrossPurchaseAmount = payment.PurchaseAmount + payment.CoachFee;


            var clientPurchaseVm = _mapper.Map<PurchaseViewModel>(clientPurchase);
            switch (clientPurchase.ContributionType)
            {
                case nameof(ContributionCourse):
                    var isPaidAsEntireCourse = clientPurchaseVm.IsPaidAsEntireCourse;
                    payment.TotalCost = _cohealerIncomeService.CalculateTotalCostForContibutionCourse(isPaidAsEntireCourse,
                    contribution as ContributionCourse);
                    break;

                case nameof(ContributionOneToOne):
                    var isPaidAsSessionPackage = clientPurchaseVm.IsPaidAsSessionPackage;
                    payment.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionOneToOne(isPaidAsSessionPackage,
                    contribution as ContributionOneToOne);
                    break;

                case nameof(ContributionMembership):
                    payment.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionMembership(payment,
                    contribution as ContributionMembership);
                    break;

                case nameof(ContributionCommunity):
                    payment.TotalCost = _cohealerIncomeService.CalculateTotalCostForContributionCommunity(payment,
                    contribution as ContributionCommunity);
                    break;

                default:
                    throw new Exception("Unsupported contribution type");
            }
        }
    }
}