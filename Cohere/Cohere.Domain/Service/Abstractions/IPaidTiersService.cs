using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Entity.Enums.Contribution;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IPaidTiersService<TViewModel, TEntity> : IServiceAsync<TViewModel, TEntity>
    {
        Task<OperationResult<PaidTierOptionViewModel>> CreatePaidTierOptionProductPlan(TViewModel paidTierOptionVm,string contributionCurrency);

        Task<OperationResult<string>> CreateCheckoutSessionSubscription(
            string paidTierId, PaidTierOptionPeriods period, string clientAccountId);

        Task<CurrentPaidTierModel> GetCurrentPaidTier(string accountId, DateTime? atDateTime = null);

        Task<CurrentPaidTierViewModel> GetCurrentPaidTierViewModel(string accountId);

        Task<OperationResult> CancelPaidTierPlan(string accountId);

        Task<IEnumerable<TViewModel>> GetAll(string accountId);

        Task<OperationResult> UpgradePaidTierPlan(string accountId, string desiredPaidTierId, PaidTierOptionPeriods newPaymentPeriod);
    }
}