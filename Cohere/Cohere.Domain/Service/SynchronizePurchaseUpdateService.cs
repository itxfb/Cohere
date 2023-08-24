using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using System;
using System.Linq;

namespace Cohere.Domain.Service
{
    public class SynchronizePurchaseUpdateService : ISynchronizePurchaseUpdateService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SynchronizePurchaseUpdateService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Here we also have contribution. Shall I create similar method for PaidTier?
        //TODO: make It async
        public void Sync(Purchase purchase)
        {
            using (var updatingLock = MachineLock.Create(purchase.Id, TimeSpan.FromMinutes(1)))
            {
                var actualPurchase = _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(x => x.ContributionId == purchase.ContributionId && x.ClientId == purchase.ClientId)
                    .GetAwaiter().GetResult();

                if (actualPurchase.UpdateTime > purchase.UpdateTime)
                {
                    var samePayments = purchase.Payments
                        .Join(actualPurchase.Payments, x => x.TransactionId, y => y.TransactionId,
                            (x, y) => x.TransactionId).ToList();
                    var diffPayments = purchase.Payments
                        .Where(x => !samePayments.Contains(x.TransactionId)).ToList();

                    actualPurchase.Payments.AddRange(diffPayments);
                    purchase.Payments = actualPurchase.Payments;
                }

                _unitOfWork.GetRepositoryAsync<Purchase>().Update(purchase.Id, purchase).GetAwaiter().GetResult();
            }
        }

        //TODO: make it Async
        public void Sync(PaidTierPurchase paidTierPurchase)
        {
            using (var updatingLock = MachineLock.Create(paidTierPurchase.Id, TimeSpan.FromMinutes(1)))
            {
                var actualPurchase = _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                    .GetOne(x => x.ClientId == paidTierPurchase.ClientId)
                    .GetAwaiter().GetResult();

                if (actualPurchase.UpdateTime > paidTierPurchase.UpdateTime)
                {
                    var samePayments = paidTierPurchase.Payments
                        .Join(actualPurchase.Payments, x => x.TransactionId, y => y.TransactionId,
                            (x, y) => x.TransactionId).ToList();
                    var diffPayments = paidTierPurchase.Payments
                        .Where(x => !samePayments.Contains(x.TransactionId)).ToList();

                    actualPurchase.Payments.AddRange(diffPayments);
                    paidTierPurchase.Payments = actualPurchase.Payments;
                }

                _unitOfWork.GetRepositoryAsync<PaidTierPurchase>().Update(paidTierPurchase.Id, paidTierPurchase)
                    .GetAwaiter().GetResult();
            }
        }
    }
}