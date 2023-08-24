using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Stripe;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service
{
    public interface IPaidTierPurchaseService
    {
        OperationResult<CreateMoneyTransferResult> CreateTransfers(Account coachAccount, Charge charge);
    }
}