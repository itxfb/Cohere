using Cohere.Entity.Repository.Abstractions.Generic;
using Cohere.Entity.Repository.Generic;

namespace Cohere.Entity.UnitOfWork
{
    public interface IUnitOfWork
    {
        IRepositoryAsync<TEntity> GetRepositoryAsync<TEntity>()
            where TEntity : BaseEntity;

        GenericRepositoryAsync<TEntity> GetGenericRepositoryAsync<TEntity>()
            where TEntity : BaseEntity;
    }
}
