using Cohere.Entity.Entities;

namespace Cohere.Domain.Service.Abstractions
{
    public interface ISynchronizePurchaseUpdateService
    {
        void Sync(Purchase purchase);

        void Sync(PaidTierPurchase paidTierPurchase);
    }
}
