using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.ContributionViewModels;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAcademyService
    {
        Task<OperationResult<List<AcademyContributionPreviewViewModel>>> GetContributionBundledWithPaidTierProductAsync();
    }
}