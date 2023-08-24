using Amazon.Runtime.Internal.Util;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Invoice;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class CommonService : ICommonService
    {
        public const string ContributionViewUrl = "ContributionView";
        private readonly SubscriptionService _subscriptionService;
        private readonly ILogger<CommonService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _contributionViewUrl;
        public CommonService(Func<string, string> contributionViewUrlResolver,
            SubscriptionService subscriptionService,
            ILogger<CommonService> logger,
            IUnitOfWork unitOfWork)
        {
            _contributionViewUrl = contributionViewUrlResolver.Invoke(ContributionViewUrl);
            _subscriptionService = subscriptionService;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }
        public string GetContributionViewUrl(string contributionId)
        {
            string url = $"{_contributionViewUrl}{contributionId}/sessions";
            return url;
        }

        public async Task<OperationResult<Subscription>> GetProductPlanSubscriptionAsync(string subscriptionId)
        {
            var retryCount = 3;
            if (subscriptionId == null)
            {
                return OperationResult<Subscription>.Failure($"'{nameof(subscriptionId)}' must be not empty.");
            }
            var options = new SubscriptionGetOptions();
            options.AddExpand("latest_invoice.payment_intent");
            options.AddExpand("latest_invoice.charge.balance_transaction");
            options.AddExpand("schedule");
            try
            {
                var subscription = await _subscriptionService.GetAsync(subscriptionId, options);
                return OperationResult<Subscription>.Success(subscription);
            }
            catch (StripeException ex)
            {
                if (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests) //If request limit reached on stripe we can atleast retry 3 times
                {
                    for (int i = 0; i < retryCount; i++)
                    {
                        try
                        {
                            var subscription = await _subscriptionService.GetAsync(subscriptionId, options);
                            return OperationResult<Subscription>.Success(subscription);
                        }
                        catch (StripeException)
                        {
                            //Ignore
                        }
                    }
                }
                _logger.LogError(ex, "error during getting product plan subscription");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }
        public async Task<PaidTierOption> GetPaidTierByPlanId(string planId)
        {
            return await _unitOfWork.GetRepositoryAsync<PaidTierOption>()
                .GetOne(p =>
                    p.PaidTierInfo.ProductMonthlyPlanId == planId
                    || p.PaidTierInfo.ProductAnnuallyPlanId == planId
                    || p.PaidTierInfo.ProductSixMonthPlanId == planId);
        }

        public DateTime? GetNextRenewelDateOfPlan(PaidTierOptionPeriods billingFrequency, DateTime planCreatedDate)
        {
            switch (billingFrequency)
            {
                case PaidTierOptionPeriods.Monthly:
                    return planCreatedDate.AddMonths(1);
                case PaidTierOptionPeriods.Annually:
                    return planCreatedDate.AddYears(1);
                case PaidTierOptionPeriods.EverySixMonth:
                    return planCreatedDate.AddMonths(6);
                default:
                    return null;
            }
        }
        public async Task<Dictionary<string, string>> GetStripeStandardAccounIdFromContribution(ContributionBase contribution)
        {
            var contributionStandardAccountDic = new Dictionary<string, string>();
            string standardAccountId = string.Empty;
            if (contribution.PaymentType == PaymentTypes.Advance)
            {
                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.Id == contribution.UserId);
                standardAccountId = user?.StripeStandardAccountId;
                contributionStandardAccountDic.Add(contribution.Id, standardAccountId);
            }
            return contributionStandardAccountDic;
        }
        public async Task<Dictionary<string, string>> GetUsersStandardAccountIdsFromPurchases(List<PurchaseViewModel> purchases)
        {
            string stripStandardAccountId = string.Empty;
            var contributionStandardAccountDic = new Dictionary<string, string>();
            var contributionIds = purchases?.Select(m => m.ContributionId).ToList().Distinct();
            var contributionsWithAdvancePayment = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(c => contributionIds.Contains(c.Id) && c.PaymentType == PaymentTypes.Advance);

            var userIds = contributionsWithAdvancePayment?.Select(m => m.UserId).ToList().Distinct();
            var users = await _unitOfWork.GetRepositoryAsync<User>().Get(m => userIds.Contains(m.Id));

            foreach (var contrib in contributionsWithAdvancePayment)
            {
                stripStandardAccountId = users?.FirstOrDefault(m => m.Id == contrib.UserId)?.StripeStandardAccountId;
                contributionStandardAccountDic.Add(contrib.Id, stripStandardAccountId);
            }
            return contributionStandardAccountDic;
        }
        public bool IsUserRemovedFromContribution(PurchaseViewModel purchase)
        {
            if (purchase?.ClientId.ToLower().Contains("delete") == true)
            {
                return true;
            }
            if (purchase?.Payments.LastOrDefault()?.IsAccessRevokedByCoach == true)
            {
                return true;
            }
            return false;
        }

        public void RemoveUserFromContributionSessions(ContributionBase contribution, string participantId)
        {
            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                // update the contribution now with the removed client
                var sessionTimes = sessionBasedContribution.Sessions.SelectMany(x => x.SessionTimes);
                //if the prticipant is removed from a contribution, should be removed from all sessions
                foreach (var sessionTime in sessionTimes)
                {
                    sessionTime.ParticipantsIds?.RemoveAll(x => x == participantId);
                    sessionTime.ParticipantInfos?.RemoveAll(x => x.ParticipantId == participantId);
                    sessionTime.CompletedSelfPacedParticipantIds?.RemoveAll(x => x == participantId);
                    sessionTime.UsersWhoViewedRecording?.RemoveAll(x => x == participantId);
                }

                //update the database
                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, sessionBasedContribution).GetAwaiter().GetResult();
            }
        }

        public StripeInvoice GetInvoiceIfExist(string clientId, string contributionId, string paymentOption)
        {
            var invoiceObj = _unitOfWork.GetRepositoryAsync<StripeInvoice>().GetOne(i => !i.IsCancelled && i.ContributionId == contributionId
                                                         && i.ClientId == clientId && i.PaymentOption == paymentOption.ToString()).GetAwaiter().GetResult();
            return invoiceObj;
        }
    }
}
