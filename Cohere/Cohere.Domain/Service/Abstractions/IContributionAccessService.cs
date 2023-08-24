using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Entity.Entities.Contrib;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContributionAccessService
    {
        Task<OperationResult<AccessCode>> CreateAccessCode(string contributionId, string accountId, int validPeriodInYears);

        Task<OperationResult> GrantAccessByAccessCode(string clientAccountId, string contributionId, string accessCode);
        
        Task<OperationResult> CancelAccess(string accountId, string contributionId, string participantId);
    }
}