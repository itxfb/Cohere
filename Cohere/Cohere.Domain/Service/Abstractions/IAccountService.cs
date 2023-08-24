using System.Threading.Tasks;

using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Service.Abstractions.Generic;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAccountService<TViewModel, TEntity> : IServiceAsync<TViewModel, TEntity>
        where TViewModel : class
    {
        Task<OperationResult<TViewModel>> GetByEmail(string email);

        Task<OperationResult<AccountPreferencesViewModel>> SetUserPreferences(string accountId, AccountPreferencesViewModel model);
    }
}
