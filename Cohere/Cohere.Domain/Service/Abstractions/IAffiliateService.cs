using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAffiliateService
    {
        Task<OperationResult<string>> GetUserNameByInviteCode(string inviteCode);

        Task<OperationResult> ShareReferralLink(IEnumerable<string> email, string inviterAccountId);

        Task<OperationResult> ToggleEnrollmentStatus(string accountId);

        Task<bool> IsEnrolled(string accountId);

        Task<OperationResult> GetPayout(string accountId, decimal amount);

        Task<OperationResult> GetFullPayout(string accountId);
    }
}
