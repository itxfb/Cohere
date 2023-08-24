using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Repository.Abstractions.Generic;

using MongoDB.Driver;

namespace Cohere.Entity.Repository.Generic
{
    public class GenericRepositoryAsync<T> : IRepositoryAsync<T>
        where T : BaseEntity
    {
        public readonly IMongoCollection<T> Collection;

        public GenericRepositoryAsync(IMongoCollection<T> collection)
        {
            Collection = collection;
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            var entitiesCursored = await Collection.FindAsync(d => true);
            return entitiesCursored.ToList();
        }

        public async Task<IEnumerable<T>> Get(Expression<Func<T, bool>> predicate)
        {
            var entitiesCursored = await Collection.FindAsync(predicate);
            return entitiesCursored.ToList();
        }
        public async Task<int> GetCount(Expression<Func<T, bool>> predicate)
        {
            return Convert.ToInt32(await Collection.CountDocumentsAsync(predicate));
        }
        public async Task<IEnumerable<T>> GetSkipTake(Expression<Func<T, bool>> predicate, int skip,int take)
        {
            return await Collection.Find(predicate).Skip(skip).Limit(take).ToListAsync();
        }
        public async Task<IEnumerable<T>> GetSkipTakeWithSort(Expression<Func<T, bool>> predicate, int skip, int take, OrderByEnum orderByEnum)
        {
            if (orderByEnum == OrderByEnum.Asc)
            {
                return await Collection.Find(predicate).SortBy(m => m.CreateTime).Skip(skip).Limit(take).ToListAsync();
            }
            else
            {
                return await Collection.Find(predicate).SortByDescending(m => m.CreateTime).Skip(skip).Limit(take).ToListAsync();
            }
        }
        public async Task<T> GetOne(Expression<Func<T, bool>> predicate)
        {
            var entitiesCursored = await Collection.FindAsync(predicate);
            return await entitiesCursored.FirstOrDefaultAsync();
        }

        public async Task<long> Count(Expression<Func<T, bool>> predicate)
        {
            return await Collection.CountDocumentsAsync(predicate);
        }

        public virtual async Task<T> Insert(T entity)
        {
           entity.CreateTime = DateTime.UtcNow;
           entity.UpdateTime = DateTime.UtcNow;
           await Collection.InsertOneAsync(entity);

            // Once inserted MongoDB driver will update entity with Id
            return entity;
        }

        // TODO: Remove updateCreateTime
        public virtual async Task<T> Update(string id, T updatedEntity, bool updateCreateTime = false, bool updateUpdatedTime=true)
        {
            var existedEntityCursored = await Collection.FindAsync(e => e.Id == id);
            var existedEntity = await existedEntityCursored.FirstOrDefaultAsync();

            updatedEntity.CreateTime = updateCreateTime ? updatedEntity.CreateTime : existedEntity.CreateTime;
            if(updateUpdatedTime)
                updatedEntity.UpdateTime = DateTime.UtcNow;
            await Collection.ReplaceOneAsync(e => e.Id == id, updatedEntity);
            return updatedEntity;
        }

        public async Task<long> Delete(string id)
        {
            var result = await Collection.DeleteOneAsync(userProfile => userProfile.Id == id);
            return result.DeletedCount;
        }

        public async Task<long> Delete(T entity)
        {
            if (entity != null)
            {
                var result = await Collection.DeleteOneAsync(userProfile => userProfile.Id == entity.Id);
                return result.DeletedCount;
            }
            else
            {
                throw new NullReferenceException("Entity to delete is null");
            }
        }
    }
}
