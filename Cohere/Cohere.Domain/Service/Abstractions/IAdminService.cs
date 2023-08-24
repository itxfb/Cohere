using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.AdminViewModels;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.Payment;
using Cohere.Entity.Entities;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAdminService
    {
        Task<OperationResult<KpiReportResultViewModel>> GetKpiReportAsync(KpiReportRequestViewModel viewModel); 
        Task<OperationResult<ActiveCampaignReportResultViewModel>> GetActiveCampaignReportAsync();
        Task<OperationResult<IEnumerable<PurchasesWithCouponCodeViiewModel>>> GetPurchasesWithCouponCode();
        Task<OperationResult<IEnumerable<Purchase>>> UpdateAllClientPurchasesWithStripeData(bool previewOnly);
    }
}