using System;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using Stripe;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service
{
    public class PaidTierPurchaseService : IPaidTierPurchaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPayoutService _payoutService;
        private readonly IAffiliateCommissionService _affiliateCommissionService;
        private readonly IStripeService _stripeService;

        public PaidTierPurchaseService(
            IAffiliateCommissionService affiliateCommissionService,
            IStripeService stripeService,
            IUnitOfWork unitOfWork,
            IPayoutService payoutService)
        {
            _affiliateCommissionService = affiliateCommissionService;
            _stripeService = stripeService;
            _unitOfWork = unitOfWork;
            _payoutService = payoutService;
        }

        public OperationResult<CreateMoneyTransferResult> CreateTransfers(
            Account coachAccount,
            Charge charge)
        {
            try
            {
                if (coachAccount.InvitedBy == null)
                {
                    return new OperationResult<CreateMoneyTransferResult>(
                        true, "The coach was not invited by anyone");
                }

                var availableAffiliateRevenue = _affiliateCommissionService
                    .GetAffiliateIncomeAsLong(charge.BalanceTransaction.Net / _stripeService.SmallestCurrencyUnit, 100m, coachAccount.Id).GetAwaiter()
                    .GetResult();

                if (availableAffiliateRevenue <= 0L)
                {
                    return new OperationResult<CreateMoneyTransferResult>(
                        true,
                        "The available affiliate revenue is less than 0");
                }

                var affiliateUser = _unitOfWork.GetRepositoryAsync<User>()
                    .GetOne(e => e.AccountId == coachAccount.InvitedBy).GetAwaiter().GetResult();

                var affiliateConnectedStripeAccountId = affiliateUser.ConnectedStripeAccountId;

                var transferResult = _payoutService.CreateTransferAsync(
                    charge.Id,
                    affiliateConnectedStripeAccountId,
                    availableAffiliateRevenue,charge.Currency).GetAwaiter().GetResult();

                return new OperationResult<CreateMoneyTransferResult>(
                    transferResult.Succeeded,
                    transferResult.Message,
                    new CreateMoneyTransferResult
                    {
                        AffiliateTransfer = (Transfer)transferResult.Payload
                    });
            }
            catch (Exception e)
            {
                return new OperationResult<CreateMoneyTransferResult>(false, e.Message);
            }
        }
    }
}