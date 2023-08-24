using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Account;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAuthService
    {
        Task<OperationResult<AccountAndUserWithRolesAggregateViewModel>> GetUserData(string accountId);

        Task<OperationResult> SignInAsync(LoginViewModel loginVm, bool lockoutOnFailure);
    }
}
