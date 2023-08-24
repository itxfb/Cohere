using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums.Contribution;
using Stripe;
using Stripe.Checkout;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IStripeService
    {
        string Currency { get; }

        decimal SmallestCurrencyUnit { get; }

        string StripePublishableKey { get; }

        Task<OperationResult<PaymentIntent>> CreatePaymentIntentAsync(PaymentIntentCreateViewModel model,string contributionCurrency,string ConnectedStripeAccountId);

        Task<OperationResult> TransferFundsToAccountAsync(
            long amount,
            string destination,
            string relatedPaymentIntentId, string contributionCurrency);

        Task<OperationResult<Subscription>> SubscribeToProductPlanAsync(
            ProductSubscriptionViewModel model,
            string contributionId);

        Task<OperationResult> FinalizeInvoiceAsync(string invoiceId, string standardAccountId = null);

        Task<OperationResult> VoidInvoiceAsync(string invoiceId, string standardAccountId = null);

        Task<OperationResult> GetProductPlanSubscriptionAsync(GetPlanSubscriptionViewModel model);

        Task<OperationResult<Subscription>> GetProductPlanSubscriptionAsync(string subscriptionId, string standardAccountId = null);

        Task<OperationResult<string>> CreateProductAsync(CreateProductViewModel model);
        Task<OperationResult<Product>> GetProductAsync(string productId, string standardAccountId = null);

        Task<OperationResult<string>> CreateProductWithTaxablePlanAsync(CreateProductWithTaxblePlaneViewModel model);
        Task<OperationResult<string>> CreateProductPlanAsync(CreateProductPlanViewModel model, string contributionCurrency, string standardAccountId = null);

        Task<OperationResult<Plan>> GetProductPlanAsync(string planId, string standardAccountId = null);

        Task<PaymentIntent> GetPaymentIntentAsync(string id, string standardAccountId = null);
        
        Task<OperationResult<Invoice>> CreateInvoiceForSinglePayment(CreateCheckoutSessionModel model);
        Task<OperationResult<Invoice>> CreateInvoiceForSubscription(CreateCheckoutSessionModel model);
        Task<Invoice> GetInvoiceAsync(string id, string standardAccountId = null);

        Task<StripeList<Invoice>> GetAllInvoiceAsync(string customerId, string standardAccountId = null);

        Task<OperationResult<PaymentIntent>> UpdatePaymentIntentAsync(PaymentIntentUpdateViewModel model);

        Task<OperationResult<Subscription>> UpdateSubscriptionProductPlanAsync(string subscriptionId, string planId);

        Task<OperationResult> UpdatePaymentIntentPaymentMethodAsync(UpdatePaymentMethodViewModel model, string standardAccountId = null);

        Task<OperationResult> CancelProductPlanSubscriptionAsync(string subscriptionId);

        Task<OperationResult> CancelProductPlanSubscriptionScheduleAsync(string subscriptionScheduleId, string standardAccountId = null);

        Task<OperationResult<Subscription>> UpdateProductPlanSubscriptionPaymentMethodAsync(
            UpdatePaymentMethodViewModel model, string standardAccountId = null);

        Task<OperationResult> UpdateProductPlanSubscriptionSchedulePaymentMethodAsync(
            string subscriptionScheduleId, string planId, string paymentMethod, long iterations);

        Task<OperationResult> CancelPaymentIntentAsync(string id, string standardAccountId= null);

        Task<OperationResult<string>> CreateCheckoutSessionToUpdatePaymentMethod(string stripeCustomerId);

        Task<OperationResult<string>> CreateCheckoutSessionSubscription(
            string stripeCustomerId, string priceId, PaidTierOption paidTierOption, string coachStripeAccount);

        Task<OperationResult<string>> CreateCustomerPortalLink(string stripeCustomerId);

        Task<OperationResult<Stripe.Checkout.Session>> CreateSubscriptionCheckoutSession(CreateCheckoutSessionModel model);

        Task<OperationResult> CancelSubscriptionAtPeriodEndAsync(string subscriptionId);

        Task<OperationResult> UpgradeSubscriptionPlanAsync(string subscriptionId, string newPlanId);

        Task<OperationResult> UpgradePaidTierPlanAsync(string subscriptionId, string newPlanId);

        Task<OperationResult<Subscription>> ScheduleSubscribeToProductPlanAsync(ProductSubscriptionViewModel model,
            string contributionId, string paymentOption);

        Task<OperationResult<Subscription>> CreateTrialSubscription(TrialSubscriptionViewModel model);

        Task<OperationResult<Subscription>> CreateTrialSubscription(ProduceTrialSubscriptionViewModel model, string contributionCurrency);

        Task<OperationResult> CancelSubscriptionImmediately(string subscriptionId, string standardAccountId = null);
        
        Task<OperationResult> RevokeContributionAccessOnSubscriptionCancel(Event @event);

        Task<string> CreatePriceForProductPaymentOptionAsync(string productId,
            decimal cost,
            PaymentOptions paymentOption, string contributionCurrency, string standardAccountId = null, TaxTypes taxType = TaxTypes.No);

        Task<string> GetPriceForProductPaymentOptionAsync(string productId, PaymentOptions paymentOptions, decimal totalDue, string standardAccountId = null);
        Task<string> GetPriceForProductRecurringPaymentAsync(string productId, string interval, string standardAccountId = null);
        Task<OperationResult<Session>> CreateCheckoutSessionSinglePayment(CreateCheckoutSessionModel model);

		Task<OperationResult<StripeList<ApplicationFee>>> GetApplicationFeesAsync(ApplicationFeeListOptions options);

        Task<OperationResult> AgreeToStripeAgreement(string stripeConnectedAccountId, string ipAddress);

        RequestOptions GetStandardAccountRequestOption(string standardAccountId);

        Task<string> CreateTaxableRecurringPriceForProduct(string productId,
         decimal cost, string contributionCurrency, string interval, int intervalCount, string standardAccountId, TaxTypes taxType, Dictionary<string, string> metadata);
    }
}
