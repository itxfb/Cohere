using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Models.Payment;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;

namespace Cohere.Domain.Service.Abstractions
{
    public interface ICohealerIncomeService
    {
        Task<IEnumerable<PurchaseIncomeViewModel>> GetDashboardIncomeAsync(string accountId);

        Task<PurchaseIncomeViewModel> GetTotalIncomeAsync(string accountId);

        Task<decimal> GetContributionRevenueAsync(string contributionId);
        Task<decimal> GetSingleClientRevenueAsync(string contributionId, string clientId);

        Task<IEnumerable<ContributionSaleViewModel>> GetContributionSalesAsync(string accountId);

        decimal CalculateTotalCostForContibutionCourse(bool isPaidAsEntireCourse, ContributionCourse contribution);

        decimal CalculateTotalCostForContributionOneToOne(bool isPaidAsSessionPackage, ContributionOneToOne contribution);

        decimal CalculateTotalCostForContributionMembership(PurchasePayment payment, ContributionMembership contributionMembership);

        decimal CalculateTotalCostForContributionCommunity(PurchasePayment payment, ContributionCommunity contributionMembership);
    }
}
