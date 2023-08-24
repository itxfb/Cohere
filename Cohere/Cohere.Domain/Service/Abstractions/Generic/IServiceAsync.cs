using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;

namespace Cohere.Domain.Service.Abstractions.Generic
{
    public interface IServiceAsync<TViewModel, TEntity>
    {
        Task<IEnumerable<TViewModel>> GetAll();

        Task<OperationResult> Insert(TViewModel accountVm);

        Task<OperationResult> Update(TViewModel obj);

        Task<OperationResult> Delete(string id);

        Task<TViewModel> GetOne(string id);

        Task<IEnumerable<TViewModel>> Get(Expression<Func<TEntity, bool>> predicate);
    }
}