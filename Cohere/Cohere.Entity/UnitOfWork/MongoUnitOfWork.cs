using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.Repository.Abstractions.Generic;
using Cohere.Entity.Repository.Generic;

using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Cohere.Entity.UnitOfWork
{
    public class MongoUnitOfWork : IUnitOfWork
    {
        private readonly IMongoDatabase _database;
        private readonly Dictionary<string, string> _collectionNames;

        private ConcurrentDictionary<Type, object> _repositoriesAsync = new ConcurrentDictionary<Type, object>();

        public MongoUnitOfWork(IOptions<MongoSecretsSettings> mongoSecrets, IOptions<MongoSettings> mongoSettings)
        {
            var client = new MongoClient(mongoSecrets.Value.MongoConnectionString);
            _database = client.GetDatabase(mongoSettings.Value.DatabaseName);
            _collectionNames = mongoSettings.Value.CollectionNames;
        }

        public IRepositoryAsync<TEntity> GetRepositoryAsync<TEntity>()
            where TEntity : BaseEntity
        {
            return (IRepositoryAsync<TEntity>)_repositoriesAsync.GetOrAdd(typeof(TEntity), (collectionType) =>
            {
                var name = _collectionNames[collectionType.Name];
                var collection = _database.GetCollection<TEntity>(name);
                return new GenericRepositoryAsync<TEntity>(collection);
            });
        }

        public GenericRepositoryAsync<TEntity> GetGenericRepositoryAsync<TEntity>()
            where TEntity : BaseEntity
        {
            return (GenericRepositoryAsync<TEntity>)_repositoriesAsync.GetOrAdd(typeof(TEntity), (collectionType) =>
            {
                var name = _collectionNames[collectionType.Name];
                var collection = _database.GetCollection<TEntity>(name);
                return new GenericRepositoryAsync<TEntity>(collection);
            });
        }
    }
}