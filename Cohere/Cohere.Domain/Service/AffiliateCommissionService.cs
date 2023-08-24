using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;

using Stripe;

using static Cohere.Domain.Utils.Constants.Stripe;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service
{
    public class AffiliateCommissionService : IAffiliateCommissionService
    {
        private const long AffiliateFee = 50L; // 50% of platform fee
        private const long FromReferralPaidTierFee = 50L; // 50% for Affiliate from bought PaidTier on Referral Account

        private static readonly long SmallestCurrencyUnit = 100L; // 100 cents
        private static readonly decimal MaxRevenuePerReferral = 500; // 500 $

        private static readonly int MaxPeriod = 12; //12 Month
        private readonly IPricingCalculationService _pricingCalculationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PayoutService _payoutService;

        public AffiliateCommissionService(
            IPricingCalculationService pricingCalculationService,
            IUnitOfWork unitOfWork,
            PayoutService payoutService)
        {
            _pricingCalculationService = pricingCalculationService;
            _unitOfWork = unitOfWork;
            _payoutService = payoutService;
        }

        public async Task<OperationResult<AffiliateRevenueLimitsModel>> GetAffiliateRevenueLimitsByInviteCode(
            string inviteCode)
        {
            var affiliateAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(e => e.Id == inviteCode);

            var affiliateConfig = affiliateAccount.AffiliateProgramConfiguration;

            var limits = new AffiliateRevenueLimitsModel()
            {
                MaxRevenue = affiliateConfig.MaxPeriodPerReferal,
                MaxPeriod = affiliateConfig.MaxPeriodPerReferal
            };

            return OperationResult<AffiliateRevenueLimitsModel>.Success(limits);
        }

        public OperationResult<AffiliateProgramConfigurationModel> GetDefaultAffiliateProgramConfiguration()
        {
            var mock = new AffiliateProgramConfigurationModel // TODO: Mocked with default values till Phase 2
            {
                AffiliateFee = AffiliateFee,
                FromReferralPaidTierFee = FromReferralPaidTierFee,
                MaxRevenuePerReferral = MaxRevenuePerReferral,
                MaxPeriodPerReferal = MaxPeriod
            };

            return OperationResult<AffiliateProgramConfigurationModel>.Success(mock);
        }

        public async Task<long> GetAffiliateIncomeAsLong(decimal purchaseAmountInCents, decimal platformPercentageFee, string referralAccountId)
        {
            return decimal.ToInt64(await GetAffiliateIncomeInCents(purchaseAmountInCents, platformPercentageFee, referralAccountId));
        }

        public async Task<decimal> GetAffiliateIncomeInCents(decimal purchaseAmountInCents, decimal platformPercentageFee, string referralAccountId)
        {
            var referralAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == referralAccountId);
            var affiliateAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == referralAccount.InvitedBy);

            var platformFeeInCents = _pricingCalculationService.CalculatePlatformIncome(purchaseAmountInCents, platformPercentageFee);
            var calculatedAffiliateIncome = platformFeeInCents * (affiliateAccount.AffiliateProgramConfiguration.AffiliateFee / 100m);

            var affiliateIncomeInCents = await GetRemainingIncomeInCents(referralAccount, affiliateAccount, calculatedAffiliateIncome);

            return _pricingCalculationService.TruncatePrice(affiliateIncomeInCents);
        }

        private async Task<decimal> GetRemainingIncomeInCents(
            BaseEntity referralAccount,
            Account affiliateAccount,
            decimal calculatedAffiliateIncomeInCents)
        {
            var currentRevenue = await CalculateRevenuePerReferralAsync(referralAccount.Id);

            var maxRevenuePerReferral = affiliateAccount.AffiliateProgramConfiguration.MaxRevenuePerReferral ?? MaxRevenuePerReferral;

            var remainingRevenueInCents = (maxRevenuePerReferral - currentRevenue.TotalRevenue) * SmallestCurrencyUnit;

            return Math.Max(Math.Min(remainingRevenueInCents, calculatedAffiliateIncomeInCents), 0m);
        }

        public async Task<long> GetAffiliateRevenuePayoutsAmountInCents(string coachAccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == coachAccountId);

            var connectedStripeAccountId = user.ConnectedStripeAccountId;

            var allPayouts = await _payoutService.ListAsync(
                requestOptions: new RequestOptions { StripeAccount = connectedStripeAccountId });

            return allPayouts.Where(e => e.Metadata != null
                                         && e.Metadata.ContainsKey(MetadataKeys.IsAffiliateRevenue)
                                         && e.Metadata[MetadataKeys.IsAffiliateRevenue] == bool.TrueString)
                .Sum(e => e.Amount);
        }

        public async Task<OperationResult<AffiliateRevenueModel>> GetAffiliateRevenueSummaryAsync(string accountId)
        {
            var affiliateAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(e => e.Id == accountId);

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == accountId);
            AffiliateRevenueModel result = new AffiliateRevenueModel();

            var referralsCount = await _unitOfWork.GetRepositoryAsync<Account>()
                    .Count(e => e.InvitedBy == affiliateAccount.Id);
            var referralsWithSalesCount = await CountCoachesWithSales(affiliateAccount.Id);

            var revenue = await CalculateRevenue(affiliateAccount.Id);
            var paidOutRevenueInCents = await GetAffiliateRevenuePayoutsAmountInCents(affiliateAccount.Id);
            var paidOutRevenue = (decimal)paidOutRevenueInCents / SmallestCurrencyUnit;
            if (paidOutRevenue > revenue.TotalRevenue)
                paidOutRevenue = revenue.TotalRevenue;
            result = new AffiliateRevenueModel
            {
                TotalRevenue = revenue.TotalRevenue,
                InEscrowRevenue = revenue.InEscrowRevenue,
                PaidOutRevenue = paidOutRevenue,
                ReferralsCount = referralsCount,
                ReferralsWithSalesCount = referralsWithSalesCount,
            };

            var affiliatesInfo = await _unitOfWork.GetRepositoryAsync<ReferralsInfo>().Get(a => a.ReferralUserId == user.Id);
            result.InEscrowRevenue = affiliatesInfo.Where(a => (DateTime.UtcNow - a.TransferTime).TotalDays < 60).ToList().Sum(s => s.ReferralAmount);
            result.PaidOutRevenue += affiliatesInfo.Where(s => s.IsPaidOut).Sum(a => a.ReferralAmount);
            if (revenue.TotalRevenue == 0)
                revenue.TotalRevenue = affiliatesInfo.Sum(a => a.ReferralAmount);
            result.AvailableToPayoutRevenue = revenue.TotalRevenue - result.InEscrowRevenue - result.PaidOutRevenue;

            return OperationResult<AffiliateRevenueModel>.Success(result);
        }

        public async Task<AffiliateRevenueModelBase> CalculateRevenuePerReferralAsync(string referralAccountId)
        {
            var referralUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == referralAccountId);

            var allContributionPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(e => e.ContributorId == referralUser.Id && e.IsFirstPaymentHandeled);

            var allPaidTierPurchases = await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(p => p.ClientId == referralUser.Id && p.IsFirstPaymentHandled);

            return CalculateRevenue(allContributionPurchases, allPaidTierPurchases);
        }

        private async Task<AffiliateRevenueModelBase> CalculateRevenue(string affiliateAccountId)
        {
            var allReferredUserIds = await GetAllReferredUserIds(affiliateAccountId);

            var allContributionPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(e => allReferredUserIds.Contains(e.ContributorId) && e.IsFirstPaymentHandeled);

            var allPaidTierPurchases = await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(p => allReferredUserIds.Contains(p.ClientId) && p.IsFirstPaymentHandled);

            return CalculateRevenue(allContributionPurchases, allPaidTierPurchases);
        }

        private static AffiliateRevenueModelBase CalculateRevenue(
            IEnumerable<Purchase> contributionPurchases,
            IEnumerable<PaidTierPurchase> paidTierPurchases)
        {
            var allContributionPaymentsWithAffiliateTransfers = contributionPurchases
                .SelectMany(e => e.Payments
                    .Where(j =>
                        j.PaymentStatus == PaymentStatus.Succeeded
                        && j.AffiliateRevenueTransfer != null))
                .ToList();

            var allPaidTierPaymentsWithAffiliateTransfers = paidTierPurchases
                .SelectMany(p => p.Payments
                    .Where(pt => pt.PaymentStatus == PaymentStatus.Paid && pt.AffiliateRevenueTransfer != null));

            var inEscrow = allContributionPaymentsWithAffiliateTransfers
                .Where(e => e.AffiliateRevenueTransfer.IsInEscrow)
                .Sum(e => e.AffiliateRevenueTransfer.Amount);

            var totalForContribution = allContributionPaymentsWithAffiliateTransfers
                .Sum(e => e.AffiliateRevenueTransfer.Amount);

            var totalForPaidTier =
                allPaidTierPaymentsWithAffiliateTransfers
                    .Sum(p => p.AffiliateRevenueTransfer.Amount);

            return new AffiliateRevenueModelBase
            {
                TotalRevenue = totalForContribution + totalForPaidTier,
                InEscrowRevenue = inEscrow
            };
        }

        private async Task<long> CountCoachesWithSales(string affiliateAccountId)
        {
            var allReferredUserIds = await GetAllReferredUserIds(affiliateAccountId);

            var allSuccessPurchases = await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(e => allReferredUserIds.Contains(e.ClientId) && e.IsFirstPaymentHandled);

            return allSuccessPurchases.Select(e => e.ClientId).Distinct().Count();
        }

        private async Task<IList<string>> GetAllReferredUserIds(string affiliateAccountId)
        {
            var allReferredAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(e => e.InvitedBy == affiliateAccountId);

            var allReferredAccountIds = allReferredAccount.Select(e => e.Id).ToList();

            var allReferredUser = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(e => allReferredAccountIds.Contains(e.AccountId));

            return allReferredUser.Select(e => e.Id).ToList();
        }
    }
}