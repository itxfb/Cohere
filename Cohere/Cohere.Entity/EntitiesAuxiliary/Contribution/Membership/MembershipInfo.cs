using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.Enums.Contribution;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution.Membership
{
    public class MembershipInfo
    {
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        public Dictionary<PaymentOptions, decimal> Costs { get; set; } = new Dictionary<PaymentOptions, decimal>();

        public MembershipPackage MembershipPackage { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        public Dictionary<PaymentOptions, BillingPlanInfo> ProductBillingPlans = new Dictionary<PaymentOptions, BillingPlanInfo>();

        public Dictionary<string, PaymentOptions> PaymentOptionsMap => ProductBillingPlans.ToDictionary(
            e => e.Value.ProductBillingPlanId,
            e => e.Key);
    }
}