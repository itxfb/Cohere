using System.Collections.Generic;

using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Chat;
using Cohere.Entity.EntitiesAuxiliary.User;

using Cohere.Entity.Enums.User;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;

namespace Cohere.Entity.Infrastructure
{
    public static class MongoMapRegistrator
    {
        // Use mapping with code (not attributes) is more generic way if MongoDB will be one day replaces by other DB.
        public static void RegisterMaps()
        {
            var pack = new ConventionPack
            {
                new MongoIgnoreDefaultValuesConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true)
            };
            ConventionRegistry.Register("Custom ignore default", pack, t => true);

            BsonClassMap.RegisterClassMap<ClientPreferences>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.Curiosities)
                    .SetSerializer(
                        new DictionaryInterfaceImplementerSerializer<Dictionary<string, PreferenceLevels>>(DictionaryRepresentation.Document));
                cm.MapMember(c => c.Experiences)
                    .SetSerializer(
                        new DictionaryInterfaceImplementerSerializer<Dictionary<string, PreferenceLevels>>(DictionaryRepresentation.Document));
                cm.MapMember(c => c.Interests)
                    .SetSerializer(
                        new DictionaryInterfaceImplementerSerializer<Dictionary<string, PreferenceLevels>>(DictionaryRepresentation.Document));
            });

            BsonClassMap.RegisterClassMap<Account>(cm =>
            {
                cm.AutoMap();
                cm.UnmapMember(a => a.DecryptedPassword);
                cm.MapMember(a => a.SecurityAnswers)
                    .SetSerializer(
                        new DictionaryInterfaceImplementerSerializer<Dictionary<string, string>>(
                            DictionaryRepresentation.Document));
            });

            BsonClassMap.RegisterClassMap<User>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(a => a.BirthDate)
                    .SetSerializer(new DateTimeSerializer(true));
            });

            BsonClassMap.RegisterClassMap<ContributionBase>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.Gender)
                    .SetIgnoreIfDefault(false);
            });
            BsonClassMap.RegisterClassMap<ContributionCourse>();
            BsonClassMap.RegisterClassMap<ContributionOneToOne>();
            BsonClassMap.RegisterClassMap<ContributionMembership>();
            BsonClassMap.RegisterClassMap<ContributionCommunity>();

            BsonClassMap.RegisterClassMap<ChatConversation>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.LastMessageIndex).SetIgnoreIfDefault(false);
                cm.MapMember(c => c.LastMessageAddedTimeUtc).SetIgnoreIfDefault(false);
            });

            BsonClassMap.RegisterClassMap<ChatUserReadInfo>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(u => u.LastReadMessageIndex).SetIgnoreIfDefault(false);
                cm.MapMember(u => u.LastReadMessageTimeUtc).SetIgnoreIfDefault(false);
            });

            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(c => c.Id).SetIdGenerator(StringObjectIdGenerator.Instance);
            });
        }
    }
}
