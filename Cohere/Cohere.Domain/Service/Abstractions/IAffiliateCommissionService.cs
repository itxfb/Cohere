using System.Threading.Tasks;

using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAffiliateCommissionService
    {
        Task<OperationResult<AffiliateRevenueLimitsModel>> GetAffiliateRevenueLimitsByInviteCode(string inviteCode);

        Task<long> GetAffiliateRevenuePayoutsAmountInCents(string accountId);

        Task<OperationResult<AffiliateRevenueModel>> GetAffiliateRevenueSummaryAsync(string accountId);

        Task<AffiliateRevenueModelBase> CalculateRevenuePerReferralAsync(string referralAccountId);

        Task<long> GetAffiliateIncomeAsLong(decimal purchaseAmountInCents, decimal platformPercentageFee, string referralAccountId);

        Task<decimal> GetAffiliateIncomeInCents(decimal purchaseAmountInCents, decimal platformPercentageFee, string referralAccountId);

        OperationResult<AffiliateProgramConfigurationModel> GetDefaultAffiliateProgramConfiguration();
    }
}
