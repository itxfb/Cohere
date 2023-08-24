using System;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Entity.Entities;
using Stripe;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Entities.Contrib;
using Cohere.Domain.Models.Payment;
using Cohere.Entity.Entities.Invoice;

namespace Cohere.Domain.Service.Abstractions
{
    public interface ICommonService
    {
        string GetContributionViewUrl(string contributionId);
        Task<OperationResult<Subscription>> GetProductPlanSubscriptionAsync(string subscriptionId);
        Task<PaidTierOption> GetPaidTierByPlanId(string planId);
        DateTime? GetNextRenewelDateOfPlan(PaidTierOptionPeriods billingFrequency, DateTime planCreatedDate);
        Task<Dictionary<string, string>> GetStripeStandardAccounIdFromContribution(ContributionBase contribution);
        Task<Dictionary<string, string>> GetUsersStandardAccountIdsFromPurchases(List<PurchaseViewModel> purchases);
        bool IsUserRemovedFromContribution(PurchaseViewModel purchase);
        void RemoveUserFromContributionSessions(ContributionBase contribution, string participantId);
        StripeInvoice GetInvoiceIfExist(string clientId, string contributionId, string paymentOption);
    }
}
