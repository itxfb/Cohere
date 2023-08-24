using System;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Stripe;

namespace Cohere.Domain.Service
{
    public class InvoicePaymentFailedEventEventService : IInvoicePaymentFailedEventService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStripeService _stripeService;
        private readonly SubscriptionService _subscriptionService;
        private readonly ISynchronizePurchaseUpdateService _synchronizePurchaseUpdateService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly INotificationService _notificationService;
        private readonly ICommonService _commonService;

        public InvoicePaymentFailedEventEventService(
            IUnitOfWork unitOfWork,
            IStripeService stripeService,
            ISynchronizePurchaseUpdateService synchronizePurchaseUpdateService,
            SubscriptionService subscriptionService,
            IActiveCampaignService activeCampaignService,
            INotificationService notificationService,
            ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _stripeService = stripeService;
            _synchronizePurchaseUpdateService = synchronizePurchaseUpdateService;
            _subscriptionService = subscriptionService;
            _activeCampaignService = activeCampaignService;
            _notificationService = notificationService;
            _commonService = commonService;
        }

        public OperationResult HandleInvoiceFailedStripeEvent(Event @event)
        {
            if (!(@event.Data.Object is Invoice invoice))
            {
                return OperationResult.Failure(
                    $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
            }

            var isSubscription = invoice.SubscriptionId != null;
            var subscription = _subscriptionService.GetAsync(invoice.SubscriptionId).GetAwaiter().GetResult();
            if (isSubscription)
            {
                subscription.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.PaidTierId, out var paidTierId);

                if (paidTierId != null)
                {
                    return HandleCheckoutSessionPaidTierFailedEvent(@event);
                }

                subscription.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.ContributionId, out var contributionId);
                if (contributionId != null)
                {
                    return HandleCheckoutSessionContributionFailedEvent(@event);
                }

                return OperationResult.Failure("The service was unable to find any purchased plans or contributions");
            }

            return OperationResult.Failure(
                "The service was unable to find the requested payment on subscriptionId");
        }

        private OperationResult HandleCheckoutSessionPaidTierFailedEvent(Event @event)
        {
            if (!(@event.Data.Object is Invoice invoice))
            {
                return OperationResult.Failure(
                    $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
            }

            var paidTierPurchase = _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .GetOne(e => e.SubscriptionId == invoice.SubscriptionId).GetAwaiter().GetResult();

            if (paidTierPurchase is { IsFirstPaymentHandled: true, DeclinedSubscriptionPurchase: null })
            {
                paidTierPurchase.DeclinedSubscriptionPurchase = new DeclinedSubscriptionPurchase
                {
                    LastPaymentFailedDate = DateTime.UtcNow,
                    AmountRemaining =
                        (invoice.AmountRemaining / _stripeService.SmallestCurrencyUnit).ToString("0.##"),
                    AmountDue = (invoice.AmountDue / _stripeService.SmallestCurrencyUnit).ToString("0.##"),
                    AmountPaid = (invoice.AmountPaid / _stripeService.SmallestCurrencyUnit).ToString("0.##")
                };

                _synchronizePurchaseUpdateService.Sync(paidTierPurchase);

                //send email for failed payment
                var user = _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == paidTierPurchase.ClientId).GetAwaiter().GetResult();
                var subscriptionResult = _commonService.GetProductPlanSubscriptionAsync(paidTierPurchase.SubscriptionId).GetAwaiter().GetResult();
                var subscription = subscriptionResult.Payload;
                var currentPaidtierPlan = _commonService.GetPaidTierByPlanId(subscription.Plan.Id).GetAwaiter().GetResult();
               
                var billingFrequency = currentPaidtierPlan.PaidTierInfo.GetStatus(subscription.Plan.Id);
                
                _notificationService.SendNotificationForFailedPayments(paidTierPurchase.DeclinedSubscriptionPurchase, user, billingFrequency.ToString(),
                    currentPaidtierPlan.DisplayName, paidTierPurchase.CreateTime);

                // update active campaign
                var customerStripeId = invoice.CustomerId;
                var client = _unitOfWork.GetRepositoryAsync<User>()
                    .GetOne(u => u.CustomerStripeAccountId == customerStripeId).GetAwaiter().GetResult();
                var activeCampaignDeal = new ActiveCampaignDeal();
                ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                {
                    CohereAccountId = client.AccountId,
                    PaidTierCreditCardStatus = EnumHelper<CreditCardStatus>.GetDisplayValue(CreditCardStatus.Failed),
                };
                _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
            }

            return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");
        }

        private OperationResult HandleCheckoutSessionContributionFailedEvent(Event @event)
        {
            if (@event.Data.Object is Invoice invoice)
            {
                var purchase = _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(e => e.SubscriptionId == invoice.SubscriptionId).GetAwaiter().GetResult();
                if (purchase != null)
                {
                    var contributionId = purchase.ContributionId;
                    var contribution = _unitOfWork.GetRepositoryAsync<ContributionBase>()
                        .GetOne(e => e.Id == contributionId).GetAwaiter().GetResult();

                    if (contribution is ContributionCourse && purchase.IsFirstPaymentHandeled &&
                        purchase.DeclinedSubscriptionPurchase == null) //TODO: membership here
                    {
                        purchase.DeclinedSubscriptionPurchase = new DeclinedSubscriptionPurchase
                        {
                            LastPaymentFailedDate = DateTime.UtcNow,
                            AmountRemaining =
                                (invoice.AmountRemaining / _stripeService.SmallestCurrencyUnit).ToString("0.##"),
                            AmountDue = (invoice.AmountDue / _stripeService.SmallestCurrencyUnit).ToString("0.##"),
                            AmountPaid = (invoice.AmountPaid / _stripeService.SmallestCurrencyUnit).ToString("0.##")
                        };

                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");
                }
            }

            return OperationResult.Failure(
                $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
        }
    }
}