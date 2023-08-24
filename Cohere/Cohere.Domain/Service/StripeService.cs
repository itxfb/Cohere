using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.FCM.Messaging;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Invoice;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Stripe;
using Stripe.Checkout;

namespace Cohere.Domain.Service
{
    public class StripeService : IStripeService
    {
        public const string SessionBillingUrl = "SessionBillingUrl";
        public const string CoachSessionBillingUrl = "CoachSessionBillingUrl";
        public const string ContributionViewUrl = "ContributionView";

        public string Currency => "usd";

        public decimal SmallestCurrencyUnit => 100m;

        public string StripePublishableKey { get; }

        private readonly PaymentIntentService _paymentIntentService;
        private readonly TransferService _transferService;
        private readonly SubscriptionService _subscriptionService;
        private readonly ProductService _productService;
        private readonly PriceService _priceService;
        private readonly PlanService _planService;
        private readonly SubscriptionScheduleService _subscriptionScheduleService;
        private readonly InvoiceService _invoiceService;
        private readonly SessionService _sessionService;
        private readonly ApplicationFeeService _applicationFeeService;
        private readonly BalanceTransactionService _balanceTransactionService;
        private readonly IValidator<PaymentIntentCreateViewModel> _paymentIntentCreateValidator;
        private readonly IValidator<ProductSubscriptionViewModel> _productSubscriptionValidator;
        private readonly IValidator<GetPlanSubscriptionViewModel> _getPlanSubscriptionValidator;
        private readonly IValidator<CreateProductViewModel> _createProductValidator;
        private readonly IValidator<CreateProductPlanViewModel> _createProductPlanValidator;
        private readonly IValidator<PaymentIntentUpdateViewModel> _paymentIntentUpdateValidator;
        private readonly IValidator<UpdatePaymentMethodViewModel> _updatePaymentMethodValidator;
        private readonly IContributionRootService _contributionRootService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<StripeService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly StripeAccountService _stripeAccountService;
        private readonly INotificationService _notificationService;
        private readonly InvoiceItemService _invoiceItemService;
        private readonly ICommonService _commonService;

        private readonly string _sessionBillingUrl;
        private readonly string _coachSessionBillingUrl;
        private readonly string _contributionViewUrl;

        public StripeService(
            IUnitOfWork unitOfWork,
            PaymentIntentService paymentIntentService,
            TransferService transferService,
            SubscriptionService subscriptionService,
            ProductService productService,
            PriceService priceService,
            PlanService planService,
            SubscriptionScheduleService subscriptionScheduleService,
            InvoiceService invoiceService,
            SessionService sessionService,
            ApplicationFeeService applicationFeeService,
            BalanceTransactionService balanceTransactionService,
            IValidator<PaymentIntentCreateViewModel> paymentIntentCreateValidator,
            IValidator<ProductSubscriptionViewModel> productSubscriptionValidator,
            IValidator<GetPlanSubscriptionViewModel> getPlanSubscriptionValidator,
            IValidator<CreateProductViewModel> createProductValidator,
            IValidator<CreateProductPlanViewModel> createProductPlanValidator,
            IValidator<PaymentIntentUpdateViewModel> paymentIntentUpdateValidator,
            IValidator<UpdatePaymentMethodViewModel> updatePaymentMethodValidator,
            IContributionRootService contributionRootService,
            IMemoryCache memoryCache,
            ILogger<StripeService> logger,
            Func<string, string> credentialsResolver,
            Func<string, string> sessionBillingUrlResolver,
            Func<string, string> coachSessionBillingUrlResolver,
            Func<string, string> contributionViewUrlResolver,
            StripeAccountService stripeAccountService,
            NotificationService notificationService,
            InvoiceItemService invoiceItemService,
            ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _paymentIntentService = paymentIntentService;
            _transferService = transferService;
            _subscriptionService = subscriptionService;
            _productService = productService;
            _priceService = priceService;
            _planService = planService;
            _subscriptionScheduleService = subscriptionScheduleService;
            _invoiceService = invoiceService;
            _sessionService = sessionService;
            _applicationFeeService = applicationFeeService;
            _balanceTransactionService = balanceTransactionService;
            _paymentIntentCreateValidator = paymentIntentCreateValidator;
            _productSubscriptionValidator = productSubscriptionValidator;
            _getPlanSubscriptionValidator = getPlanSubscriptionValidator;
            _createProductValidator = createProductValidator;
            _createProductPlanValidator = createProductPlanValidator;
            _paymentIntentUpdateValidator = paymentIntentUpdateValidator;
            _updatePaymentMethodValidator = updatePaymentMethodValidator;
            _contributionRootService = contributionRootService;
            _memoryCache = memoryCache;
            _logger = logger;
            StripePublishableKey = credentialsResolver.Invoke(nameof(StripePublishableKey));
            _sessionBillingUrl = sessionBillingUrlResolver.Invoke(SessionBillingUrl);
            _coachSessionBillingUrl = coachSessionBillingUrlResolver.Invoke(CoachSessionBillingUrl);
            _contributionViewUrl = contributionViewUrlResolver.Invoke(ContributionViewUrl);
            _stripeAccountService = stripeAccountService;
            _notificationService = notificationService;
            _invoiceItemService = invoiceItemService;
            _commonService = commonService;
        }

        public async Task<OperationResult<PaymentIntent>> CreatePaymentIntentAsync(PaymentIntentCreateViewModel model, string contributionCurrency,string stringConnectedStripeAccount)
        {
            if (model == null)
            {
                return OperationResult<PaymentIntent>.Failure(null);
            }
            
            var validationResult = await _paymentIntentCreateValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<PaymentIntent>.Failure(validationResult.Errors.ToString());
            }

            try
            {
                //await _emailService.SendAsync("uzair@cohere.live", "Payment Intent Creating", "");

                var options = BuildPaymentIntentCreateOptions(model, contributionCurrency);
                if (model.CouponID != null)
                {
                    //options.AddExtraParam("phases[0][coupon]", model.CouponID);
                }

                var intent = await _paymentIntentService.CreateAsync(options);
                //await _emailService.SendAsync("uzair@cohere.live", "Payment Intent Created", intent.Status+", Behalf of => "+intent.OnBehalfOf);

                return OperationResult<PaymentIntent>.Success(null, intent);
            }
            catch (StripeException ex)
            {
                //await _emailService.SendAsync("uzair@cohere.live", "Payment Intent Creation Error", ex.Message);

                _logger.LogError(ex, "error during creating payment intent");
                return OperationResult<PaymentIntent>.Failure(ex.Message);
            }
          
            PaymentIntentCreateOptions BuildPaymentIntentCreateOptions(PaymentIntentCreateViewModel model, string contributionCurrency)
            {
                if(!string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full")
                {
                    var Contribution_ID = model.Metadata.Where(pair => pair.Key == "ContributionId")
                    .Select(pair => pair.Value)
                    .FirstOrDefault();

                    var Payment_Type= model.Metadata.Where(pair => pair.Key == "PaymentOption")
                    .Select(pair => pair.Value)
                    .FirstOrDefault();

                    var stripeFeesForCoherePlatform = (((model.TotalChargedCost/SmallestCurrencyUnit) / 100) * model.Fee) + model.Fixed;
                    if (!model.CoachPaysFee)
                    {
                        stripeFeesForCoherePlatform= ((((model.TotalChargedCost / SmallestCurrencyUnit) ) + model.Fixed) / (1- model.Fee / 100)) - model.TransferAmount/100;
                    }

                    if(Payment_Type=="SessionsPackage" && model.CoachPaysFee==false)
                    {

                        stripeFeesForCoherePlatform = ((((model.TransferAmount / SmallestCurrencyUnit)) + model.Fixed) / (1 - model.Fee / 100)) - model.TransferAmount / 100;
                    }
                    decimal transfer = (model.TotalChargedCost / SmallestCurrencyUnit)?? 0;
                    decimal fee = stripeFeesForCoherePlatform ?? 0;
                    fee = (Math.Round(fee, 2, MidpointRounding.AwayFromZero));

                    var totalAmountForTransfer = Convert.ToInt64(Math.Round((transfer - fee), 2)*100);
                    
                    return new PaymentIntentCreateOptions
                    {
                        Amount = model.Amount,
                        Currency = contributionCurrency,
                        PaymentMethodTypes = new List<string> { "card" },
                        ReceiptEmail = model.ReceiptEmail,
                        Description = "Cohere payment.",
                        Customer = model.CustomerId,
                        OnBehalfOf = stringConnectedStripeAccount, // Want to create balance transaction in charged Currency
                        TransferData = new PaymentIntentTransferDataOptions
                        {
                            Amount= totalAmountForTransfer,
                            Destination = stringConnectedStripeAccount,
                        },
                        //ApplicationFeeAmount = Convert.ToInt64(stripeFeesForCoherePlatform),
                        Metadata = model.Metadata,
                    };
                }
                return new PaymentIntentCreateOptions
                {
                    Amount = model.Amount,
                    Currency = Currency,
                    PaymentMethodTypes = new List<string> { "card" },
                    ReceiptEmail = model.ReceiptEmail,
                    Description = "Cohere payment.",
                    Customer = model.CustomerId,
                    Metadata = model.Metadata,
                };
            }
        }

        public async Task<OperationResult> TransferFundsToAccountAsync(
            long amount,
            string destination,
            string relatedPaymentIntentId, string contributionCurrency)
        {
            try
            {
                var transferOpts = new TransferCreateOptions
                {
                    Amount = amount,
                    Destination = destination,
                    Currency = contributionCurrency,
                    Metadata = new Dictionary<string, string>
                    {
                        { "paymentIntentId", relatedPaymentIntentId }
                    }
                };

                var transfer = await _transferService.CreateAsync(transferOpts);
                return OperationResult.Success(null, transfer.Amount);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during funds transfer");
                return OperationResult.Failure("An error occurred during money transferring.");
            }
        }

        public async Task<OperationResult> CancelSubscriptionImmediately(string subscriptionId, string standardAccountId = null)
        {
            try
            {
                var subscription = await _subscriptionService.CancelAsync(subscriptionId, new SubscriptionCancelOptions(), GetStandardAccountRequestOption(standardAccountId));
                _memoryCache.Set("subscription_" + subscriptionId, subscription, TimeSpan.FromDays(2));
                return OperationResult.Success();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during canceling subscription");
                return OperationResult.Failure("error during canceling subscription");
            }
        }

        public async Task<OperationResult> RevokeContributionAccessOnSubscriptionCancel(Event @event)
        {
            try
            {
                var subscription = @event.Data.Object as Subscription;
                _memoryCache.Remove("subscription_" + subscription.Id);

                if (subscription.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.ContributionId, out var contributionId))
                {
                    var contribution = await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
                    if (contribution.PaymentType != Entity.Enums.Contribution.PaymentTypes.Advance || !contribution.IsInvoiced)
                    {
                        return OperationResult.Failure(null);
                    }

                    var customerResult = _stripeAccountService.GetCustomerAsync(subscription.CustomerId, @event.Account).GetAwaiter().GetResult();
                    if (customerResult.Failed)
                    {
                        return customerResult;
                    }

                    var customer = customerResult.Payload as Customer;
                    var clientAccount = await _unitOfWork.GetGenericRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Email == customer.Email);
                    var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == clientAccount.Id);
                    
                    var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(p => p.ContributionId == contributionId && p.ClientId == clientUser.Id);
                    if (purchase is not null)
                    {
                        purchase.Payments.LastOrDefault().IsAccessRevoked = true;
                        await _unitOfWork.GetRepositoryAsync<Purchase>().Update(purchase.Id, purchase);

                        _commonService.RemoveUserFromContributionSessions(contribution, clientUser.Id);

                        var invoiceExisted = _commonService.GetInvoiceIfExist(clientUser.Id, contributionId, purchase.Payments.LastOrDefault().PaymentOption.ToString());
                        if (invoiceExisted != null)
                        {
                            invoiceExisted.IsCancelled = true;
                            await _unitOfWork.GetRepositoryAsync<StripeInvoice>().Update(invoiceExisted.Id, invoiceExisted);
                        }

                        return OperationResult.Success();
                    }
                }
                return OperationResult.Failure(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return OperationResult.Failure(null);
            }
        }

        public async Task<OperationResult> CancelSubscriptionAtPeriodEndAsync(string subscriptionId)
        {
            try
            {
                var subscription = await _subscriptionService.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions()
                {
                    CancelAtPeriodEnd = true
                });
                _memoryCache.Set("subscription_" + subscriptionId, subscription, TimeSpan.FromDays(2));
                return OperationResult.Success();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during canceling subscription");
                return OperationResult.Failure("error during canceling subscription");
            }
        }

        public async Task<OperationResult<Subscription>> SubscribeToProductPlanAsync(
            ProductSubscriptionViewModel model,
            string contributionId)
        {
            if (string.IsNullOrEmpty(contributionId))
            {
                return OperationResult<Subscription>.Failure("contributionId not valid");
            }

            if (model == null)
            {
                return OperationResult<Subscription>.Failure("invalid model");
            }

            var validationResult = await _productSubscriptionValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<Subscription>.Failure(validationResult.Errors.ToString());
            }

            var options = new SubscriptionCreateOptions
            {
                Customer = model.CustomerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Plan = model.StripeSubscriptionPlanId
                    },
                },
                DefaultPaymentMethod = model.DefaultPaymentMethod,
            };

            try
            {
                var subscription = await _subscriptionService.CreateAsync(options);
                _memoryCache.Set("subscription_" + subscription.Id, subscription, TimeSpan.FromDays(2));
                return OperationResult<Subscription>.Success(subscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during subscribing to product");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<Subscription>> ScheduleSubscribeToProductPlanAsync(
            ProductSubscriptionViewModel model, string contributionId, string paymentOption)
        {
            if (string.IsNullOrEmpty(contributionId))
            {
                return OperationResult<Subscription>.Failure("contributionId not valid");
            }

            if (model == null)
            {
                return OperationResult<Subscription>.Failure(null);
            }

            var validationResult = await _productSubscriptionValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<Subscription>.Failure(validationResult.Errors.ToString());
            }

            var contribution = await _contributionRootService.GetOne(contributionId);
            var Payment_Option = contribution.PaymentInfo.PaymentOptions.First().ToString();
            if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
            {
                Payment_Option = "MonthlySessionSubscription";
            }


            var options = new SubscriptionScheduleCreateOptions
            {
                Customer = model.CustomerId,
                StartDate = SubscriptionScheduleStartDate.Now,
                EndBehavior = "cancel",
                Metadata = new Dictionary<string, string>
                {
                    { Constants.Stripe.MetadataKeys.ContributionId, contributionId },
                },
                Phases = new List<SubscriptionSchedulePhaseOptions>
                {
                    new SubscriptionSchedulePhaseOptions
                    {
                        Plans = new List<SubscriptionSchedulePhaseItemOptions>
                        {
                            new SubscriptionSchedulePhaseItemOptions
                            {
                                Plan = model.StripeSubscriptionPlanId
                                }
                        },
                        Iterations = model.Iterations,
                        DefaultPaymentMethod = model.DefaultPaymentMethod
                    }
                }
            };
            if (!(model.CouponId is null))
            {
                options.AddExtraParam("phases[0][coupon]", model.CouponId);
            }

            if (!string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full" && model.PaymentType != PaymentTypes.Advance.ToString())
            {
                //simple pay case, handle trasnfer amount
                options.AddExtraParam("phases[0][on_behalf_of]", model.ConnectedStripeAccountId);
                options.AddExtraParam("phases[0][transfer_data][destination]", model.ConnectedStripeAccountId);
                var amountToTransfer = ((model.BillingInfo.BillingPlanGrossCost - model.BillingInfo.BillingPlanPureCost) / model.BillingInfo.BillingPlanGrossCost) * 100;
                var transfer_amount_percent = Math.Round(100 - amountToTransfer, 2);
                if(model.PaymentIntent_Model.CoachPaysFee==true && Payment_Option== "MonthlySessionSubscription")
                {
                    amountToTransfer = (decimal)(model.BillingInfo.BillingPlanGrossCost - model.BillingInfo.BillingPlanTransferCost) / model.BillingInfo.BillingPlanGrossCost * 100;
                    transfer_amount_percent = Math.Round(100 - amountToTransfer, 2);
                    //check if trsanfer amount at stripe is decreasing due to round off
                    if ((model.BillingInfo.BillingPlanGrossCost * transfer_amount_percent) / 100m < model.BillingInfo.BillingPlanTransferCost)
                        transfer_amount_percent += 0.01m;
                }
                options.AddExtraParam("phases[0][transfer_data][amount_percent]", transfer_amount_percent);
            }

            options.AddExpand("subscription.latest_invoice");

            try
            {
                var subscriptionSchedule = await _subscriptionScheduleService.CreateAsync(options, GetStandardAccountRequestOption(model.StandardAccountId));

                // update the subscription metadata to have pyment option value
                try
                {
                    SubscriptionUpdateOptions subscriptionUpdateOptions = new SubscriptionUpdateOptions()
                    {
                        Metadata = new Dictionary<string, string>
                    {
                        { Constants.Stripe.MetadataKeys.PaymentOption, paymentOption }
                    }
                    };
                    await _subscriptionService.UpdateAsync(subscriptionSchedule.Subscription.Id, subscriptionUpdateOptions, GetStandardAccountRequestOption(model.StandardAccountId));
                }
                catch { }

                return OperationResult<Subscription>.Success(null, subscriptionSchedule.Subscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during subscription scheduling");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> CreateAndSendInvoice(CreateCheckoutSessionModel model)
        {
            try
            {
                var invoiceCreateOptions = new InvoiceCreateOptions()
                {
                    Customer = model.StripeCustomerId,
                    Metadata = model.GetMetadata(),
                    CollectionMethod = "send_invoice",
                    DaysUntilDue = 30,
                };

                invoiceCreateOptions.AddExtraParam("pending_invoice_items_behavior", "exclude");
                if (model.TaxType != TaxTypes.No) invoiceCreateOptions.AddExtraParam("automatic_tax[enabled]", true);
                invoiceCreateOptions.AddExtraParam("currency", model.Currency);
                var invoice = await _invoiceService.CreateAsync(invoiceCreateOptions, GetStandardAccountRequestOption(model.StripeStandardAccountId));

                var invoiceItemCreateOptions = new InvoiceItemCreateOptions
                {
                    Customer = model.StripeCustomerId,
                    Price = model.PriceId,
                    Invoice = invoice.Id,
                    Quantity = 1,
                    Metadata = model.GetMetadata()
                };
                await _invoiceItemService.CreateAsync(invoiceItemCreateOptions, GetStandardAccountRequestOption(model.StripeStandardAccountId));
                var result = await FinalizeInvoiceAsync(invoice.Id, model.StripeStandardAccountId);
                if (result.Failed)
                {
                    return OperationResult.Failure(result.Message);
                }
                var updatedInvoice = (Invoice)result.Payload;
                await _invoiceService.SendInvoiceAsync(updatedInvoice.Id, options: null, GetStandardAccountRequestOption(model.StripeStandardAccountId));
                return OperationResult.Success(string.Empty, updatedInvoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{ex.Message} - during CreateAndSendInvoice");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> FinalizeInvoiceAsync(string invoiceId, string standardAccountId = null)
        {
            if (invoiceId == null)
            {
                return OperationResult.Failure($"'{nameof(invoiceId)}' must be not empty.");
            }

            var options = new InvoiceFinalizeOptions { AutoAdvance = true };
            options.AddExpand("payment_intent");

            try
            {
                var invoice = await _invoiceService.FinalizeInvoiceAsync(invoiceId, options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null, invoice);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during finalizing invoices");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> VoidInvoiceAsync(string invoiceId, string standardAccountId = null)
        {
            if (invoiceId == null)
            {
                return OperationResult.Failure($"'{nameof(invoiceId)}' must be not empty.");
            }

            try
            {
                await _invoiceService.VoidInvoiceAsync(invoiceId,options:null, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during voiding invoice");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> GetProductPlanSubscriptionAsync(GetPlanSubscriptionViewModel model)
        {
            if (model == null)
            {
                return OperationResult.Failure(null);
            }

            var validationResult = await _getPlanSubscriptionValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult.Failure(validationResult.Errors.ToString());
            }

            var options = new SubscriptionListOptions
            {
                Customer = model.CustomerId,
                Plan = model.SubscriptionPlanId
            };

            options.AddExpand("data.schedule");

            try
            {
                var customerSubscriptionList = await _subscriptionService.ListAsync(options);
                return OperationResult.Success(null, customerSubscriptionList.SingleOrDefault());
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting product plan");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<Subscription>> GetProductPlanSubscriptionAsync(string subscriptionId, string standardAccountId = null)
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
                var subscription = await _subscriptionService.GetAsync(subscriptionId, options, GetStandardAccountRequestOption(standardAccountId));
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
                            var subscription = await _subscriptionService.GetAsync(subscriptionId, options, GetStandardAccountRequestOption(standardAccountId));
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

        /// <summary>
        /// Create product at Stripe side
        /// </summary>
        /// <returns>Product Id</returns>
        public async Task<OperationResult<string>> CreateProductAsync(CreateProductViewModel model)
        {
            if (model == null)
            {
                return OperationResult<string>.Failure(null);
            }

            var validationResult = await _createProductValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<string>.Failure(validationResult.Errors.ToString());
            }

            var options = new ProductCreateOptions
            {
                Name = model.Name,
                Id = model.Id
            };

            try
            {
                var product = await _productService.CreateAsync(options, GetStandardAccountRequestOption(model.StandardAccountId));
                return OperationResult<string>.Success(product.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating product");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<Product>> GetProductAsync(string productId, string standardAccountId = null)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return OperationResult<Product>.Failure(null);
            }

            try
            {
                var product = await _productService.GetAsync(productId, options: null, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult<Product>.Success(product);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting product");
                return OperationResult<Product>.Failure(ex.Message);
            }
        }

        private async Task<string> GetOrCreateProductAsync(string contributionId, string title, string standardAccountId = null)
        {
            try
            {
                var product = await _productService.GetAsync(contributionId, options: null, GetStandardAccountRequestOption(standardAccountId));
                return product.Id;
            }
            catch (StripeException ex)
            {
                if (ex.StripeError.Code != "resource_missing") throw;
                var product = await CreateProductAsync(new CreateProductViewModel()
                {
                    Id = contributionId,
                    Name = title,
                    StandardAccountId = standardAccountId
                });
                return product.Payload;
            }
        }
        public async Task<OperationResult<string>> CreateProductWithTaxablePlanAsync(CreateProductWithTaxblePlaneViewModel model)
        {
            if (model == null)
            {
                return OperationResult<string>.Failure(null);
            }
            var productId = await GetOrCreateProductAsync(model.Id, model.Name, model.StandardAccountId);
            var priceId = await GetPriceForProductRecurringPaymentAsync(productId, model.Interval, model.StandardAccountId) ??
                 await CreateTaxableRecurringPriceForProduct(model.Id, model.Amount, model.Currency, model.Interval, model.IntervalCount, model.StandardAccountId, model.TaxType, model.GetMetadata());
            if (priceId == null)
            {
                OperationResult<string>.Failure("error while creating the pricing on product");
            }
            return OperationResult<string>.Success("Successfull created the product with taxble plan", priceId);
        }
        public async Task<string> CreateTaxableRecurringPriceForProduct(string productId,
           decimal cost, string contributionCurrency, string interval, int intervalCount, string standardAccountId, TaxTypes taxType, Dictionary<string, string> metadata)
        {
            try
            {
                var option = new PriceCreateOptions()
                {
                    Product = productId,
                    Currency = contributionCurrency,
                    UnitAmountDecimal = (cost),
                    Metadata = metadata,
                    Recurring = new PriceRecurringOptions
                    {
                        Interval = interval,
                        IntervalCount = intervalCount,
                    }
                };
                if(taxType != TaxTypes.No)
                    option.AddExtraParam("tax_behavior", taxType.ToString().ToLower());

                var price = await _priceService.CreateAsync(option, GetStandardAccountRequestOption(standardAccountId));
                return price.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during creating price for product {productId}");
                return null;
            }
        }

        public async Task<string> CreatePriceForProductPaymentOptionAsync(string productId,
            decimal cost,
            PaymentOptions paymentOption, string contributionCurrency, string standardAccountId = null, TaxTypes taxType = TaxTypes.No)
        {
            try
            {
                var option = new PriceCreateOptions()
                {
                    Product = productId,
                    Currency = contributionCurrency,
                    UnitAmountDecimal = (cost * SmallestCurrencyUnit),
                    Metadata = new Dictionary<string, string>()
                    {
                        {Constants.Stripe.MetadataKeys.PaymentOption, paymentOption.ToString()}
                    }
                };
                if (!string.IsNullOrEmpty(standardAccountId) && taxType != TaxTypes.No)
                {
                    option.AddExtraParam("tax_behavior", taxType.ToString().ToLower());
                }

                var price = await _priceService.CreateAsync(option, GetStandardAccountRequestOption(standardAccountId));

                return price.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during creating price for product {productId}");
                return null;
            }
        }

        public async Task<string> GetPriceForProductPaymentOptionAsync(string productId, PaymentOptions paymentOptions, decimal totalDue, string standardAccountId = null)
        {
            try
            {
                var prices = await _priceService.ListAsync(new PriceListOptions()
                {
                    Product = productId
                },
                GetStandardAccountRequestOption(standardAccountId)
                );

                var targetPrice = prices.FirstOrDefault(e =>
                    e.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.PaymentOption, out var pricePaymentOption)
                    && pricePaymentOption == paymentOptions.ToString()
                    && e.UnitAmountDecimal == totalDue * 100);

                return targetPrice?.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during get price for product {productId}");
                return null;
            }
        }

        public async Task<string> GetPriceForProductRecurringPaymentAsync(string productId, string interval, string standardAccountId = null)
        {
            try
            {
                var prices = await _priceService.ListAsync(new PriceListOptions()
                {
                    Product = productId,
                    Recurring = new PriceRecurringListOptions()
                    {
                        Interval = interval
                    }
                },
                GetStandardAccountRequestOption(standardAccountId)
                );
                var targetPrice = prices.FirstOrDefault();
                return targetPrice?.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during get price for product {productId} with recurring payment");
                return null;
            }
        }

        /// <summary>
        /// Create product plan at Stripe side
        /// </summary>
        /// <returns>Product Plan Id</returns>
        public async Task<OperationResult<string>> CreateProductPlanAsync(CreateProductPlanViewModel model, string contributionCurrency, string standardAccountId = null)
        {
            if (model == null)
            {
                return OperationResult<string>.Failure(null);
            }

            var validationResult = await _createProductPlanValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<string>.Failure(validationResult.Errors.ToString());
            }

            var options = new PlanCreateOptions
            {
                Nickname = model.Name,
                Product = model.ProductId,
                Amount = model.Amount,
                Currency = contributionCurrency,
                Interval = model.Interval,
                IntervalCount = model.IntervalCount,
                Metadata = model.Metadata
            };

            try
            {
                var plan = await _planService.CreateAsync(options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult<string>.Success(plan.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating product plan");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<Plan>> GetProductPlanAsync(string planId, string standardAccountId = null)
        {
            if (planId == null)
            {
                return OperationResult<Plan>.Failure(null);
            }
            try
            {
                var plan = await _planService.GetAsync(planId, options: null, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult<Plan>.Success(string.Empty, plan);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during fetching product plan");
                return OperationResult<Plan>.Failure(ex.Message);
            }
        }

        public async Task<PaymentIntent> GetPaymentIntentAsync(string id, string standardAccountId = null)
        {
            if (id == null)
            {
                return null;
            }

            try
            {
                PaymentIntentGetOptions options = new PaymentIntentGetOptions()
                {
                    Expand = new List<string>()
                    {
                        "charges",
                        "transfer_data",
                    }
                };
                return await _paymentIntentService.GetAsync(id, options, GetStandardAccountRequestOption(standardAccountId));
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting payment Intent");
                return null;
            }
        }

        public async Task<OperationResult<Invoice>> CreateInvoiceForSinglePayment(CreateCheckoutSessionModel model)
        {
            //TODO: remove validation duplication
            if (model.ContributionId is null)
            {
                return OperationResult<Invoice>.Failure("contribution Id should not be null");
            }

            if (model.StripeCustomerId is null)
            {
                return OperationResult<Invoice>.Failure("stripe customer id should not be null");
            }

            if (model.PriceId is null)
            {
                return OperationResult<Invoice>.Failure("price should not be null");
            }

            var isBetaUser = !string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full";
            var isStandardAccountUser = model.IsStandardAccount && !string.IsNullOrEmpty(model.StripeStandardAccountId) ;
            if (!(isBetaUser || isStandardAccountUser))
            {
                return OperationResult<Invoice>.Failure("Invoice is currently available for full type serive agreement user OR standard account users");
            }

            decimal? totalCost = model.ProductCost, stripeFeesForCoherePlatform;
            if (model.DiscountPercent != null && model.PaymentOption != PaymentOptions.PerSession)
            {
                totalCost = (model.ProductCost - (model.ProductCost * model.DiscountPercent / 100));
            }

            stripeFeesForCoherePlatform = model.TotalChargedCost - totalCost;
            if (stripeFeesForCoherePlatform == 0)
            {
                if (model.CoachPaysStripeFee)
                    stripeFeesForCoherePlatform = ((totalCost / 100) * model.StripeFee) + model.FixedStripeAmount;
                else
                    stripeFeesForCoherePlatform = (totalCost + model.FixedStripeAmount) / (1 - model.StripeFee / 100) - totalCost;
            }

            decimal fee = stripeFeesForCoherePlatform ?? 0;
            var transferAmountWithoutCoupon = Convert.ToInt64(Math.Round((model.TotalChargedCost - fee), 2) * 100);
            long totalAmountForTransfer = transferAmountWithoutCoupon;
            decimal couponDiscountInPercentage = 1m;

            if (!string.IsNullOrEmpty(model.CouponId))
            {
                couponDiscountInPercentage = (100m - (decimal)model.CouponPerecent) / 100;
            }

            if (model.CouponId != null && model.CouponPerecent != null)
            {
                if (model.CoachPaysStripeFee == true)
                {
                    decimal amountWithoutDeduction = Math.Round(((decimal)transferAmountWithoutCoupon), 2);
                    var totalAmount_WithCoupon = model.TotalChargedCost * couponDiscountInPercentage;
                    var fee_WithCoupon = Math.Round((totalAmount_WithCoupon * (model.StripeFee / 100)) + model.FixedStripeAmount, 2);
                    //var percentToAmount = Convert.ToInt64(Math.Round((amountWithoutDeduction - amountWithoutDeduction / 100 * model.CouponPerecent ?? 1), 2) * 100);
                    var percentToAmount = (totalAmount_WithCoupon - fee_WithCoupon) * 100;
                    totalAmountForTransfer = (long)percentToAmount;
                }
                else
                {
                    decimal amountWithoutDeduction = Math.Round(((decimal)transferAmountWithoutCoupon / 100), 2);
                    var percentToAmount = Convert.ToInt64(Math.Round((amountWithoutDeduction - amountWithoutDeduction / 100 * model.CouponPerecent ?? 1), 2) * 100);
                    totalAmountForTransfer = percentToAmount;
                }
            }

            var invoiceResult = await CreateAndSendInvoice(model);
            if (invoiceResult.Failed)
            {
                return OperationResult<Invoice>.Failure(invoiceResult.Message);
            }

            var invoice = invoiceResult.Payload as Invoice;
            var invoiceObj = new StripeInvoice
            {
                InvoiceId = invoice.Id,
                ClientId = model.ClientId,
                ContributionId = model.ContributionId,
                PaymentOption = model.PaymentOption.ToString(),
            };
            await _unitOfWork.GetGenericRepositoryAsync<StripeInvoice>().Insert(invoiceObj);

            return OperationResult<Invoice>.Success(invoiceResult.Message, invoice);

        }

        public async Task<OperationResult<Invoice>> CreateInvoiceForSubscription(CreateCheckoutSessionModel model)
        {
            //TODO: remove validation duplication
            if (model.ContributionId is null)
            {
                return OperationResult<Invoice>.Failure("contribution Id should not be null");
            }

            if (model.StripeCustomerId is null)
            {
                return OperationResult<Invoice>.Failure("stripe customer id should not be null");
            }

            if (model.PriceId is null)
            {
                return OperationResult<Invoice>.Failure("price should not be null");
            }

            var isBetaUser = !string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full";
            var isStandardAccountUser = model.IsStandardAccount && !string.IsNullOrEmpty(model.StripeStandardAccountId);
            if (!(isBetaUser || isStandardAccountUser))
            {
                return OperationResult<Invoice>.Failure("Invoice is currently available for full type serive agreement user OR standard account users");
            }

            try
            {
                var options = new SubscriptionCreateOptions()
                {
                    Customer = model.StripeCustomerId,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Price = model.PriceId,
                            Metadata = model.GetMetadata(),
                            Quantity = 1,
                        }
                    },
                    Metadata = model.GetMetadata(),
                    DaysUntilDue = 30,
                    CollectionMethod = "send_invoice",

                };

                if(model.TaxType != TaxTypes.No) options.AddExtraParam("automatic_tax[enabled]", true);

                var subscriptionCreated = await _subscriptionService.CreateAsync(options, GetStandardAccountRequestOption(model.StripeStandardAccountId));

                var invoiceResult = await FinalizeInvoiceAsync(subscriptionCreated.LatestInvoiceId, model.StripeStandardAccountId);
                if (invoiceResult.Failed)
                {
                    return OperationResult<Invoice>.Failure(invoiceResult.Message);
                }

                await _invoiceService.SendInvoiceAsync(subscriptionCreated.LatestInvoiceId, options: null, GetStandardAccountRequestOption(model.StripeStandardAccountId));

                var invoiceObj = new StripeInvoice
                {
                    InvoiceId = subscriptionCreated.LatestInvoiceId,
                    ClientId = model.ClientId,
                    ContributionId = model.ContributionId,
                    PaymentOption = model.PaymentOption.ToString(),
                };
                await _unitOfWork.GetGenericRepositoryAsync<StripeInvoice>().Insert(invoiceObj);

                return OperationResult<Invoice>.Success(invoiceResult.Message, invoiceResult.Payload as Invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{ex.Message} - during CreateInvoiceForSubscription ");
                return OperationResult<Invoice>.Failure(ex.Message);
            }

        }

        public async Task<Invoice> GetInvoiceAsync(string id, string standardAccountId = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            try
            {
                var options = new InvoiceGetOptions()
                {
                    Expand = new List<string>()
                    {
                        "subscription",
                        "subscription.latest_invoice",
                        "payment_intent",
                        "charge",
                        "charge.balance_transaction"
                    },
                };
                return await _invoiceService.GetAsync(id, options, GetStandardAccountRequestOption(standardAccountId));
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting invoices");
                return null;
            }
        }

        public async Task<StripeList<Invoice>> GetAllInvoiceAsync(string customerId, string standardAccountId = null)
        {
            try
            {
                var options = new InvoiceListOptions()
                {
                    Customer = customerId,
                    Limit = 100,
                    Paid = true,
                    Status = "paid"
                };
                return await _invoiceService.ListAsync(
                    options, GetStandardAccountRequestOption(standardAccountId)
                );
			}
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting invoices");
                return null;
            }
        }

        //TODO: affiliate. how to update transferGroup
        public async Task<OperationResult<PaymentIntent>> UpdatePaymentIntentAsync(PaymentIntentUpdateViewModel model)
        {
            if (model == null)
            {
                return OperationResult<PaymentIntent>.Failure("Model is null");
            }

            var validationResult = await _paymentIntentUpdateValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<PaymentIntent>.Failure(validationResult.Errors.ToString());
            }

            PaymentIntentUpdateOptions options = new PaymentIntentUpdateOptions
            {
                Amount = model.Amount
            };

            if (!string.IsNullOrEmpty(model.ConnectedAccountId))
            {
                options.TransferData = new PaymentIntentTransferDataOptions
                {
                    Amount = model.TransferAmount,
                    Destination = model.ConnectedAccountId
                };
            }
            

            try
            {
                var intent = await _paymentIntentService.UpdateAsync(model.Id, options);
                return OperationResult<PaymentIntent>.Success(null, intent);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during updating payment intent");
                return OperationResult<PaymentIntent>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> UpgradeSubscriptionPlanAsync(string subscriptionId, string newPlanId)
        {
            try
            {
                var subscription = await _subscriptionService.GetAsync(subscriptionId);

                if (subscription.Status != "active")
                {
                    return OperationResult.Failure("Only active subscription can be upgraded");
                }

                if (subscription.Plan.Id == newPlanId)
                {
                    return OperationResult.Failure("New Plan should be different from current Plan");
                }

                var options = new SubscriptionScheduleCreateOptions
                {
                    FromSubscription = subscription.Id,
                };

                var subscriptionSchedule = await _subscriptionScheduleService.CreateAsync(options);

                var updateSubscriptionScheduleOptions = new SubscriptionScheduleUpdateOptions
                {
                    Phases = new List<SubscriptionSchedulePhaseOptions>
                    {
                        new SubscriptionSchedulePhaseOptions
                        {
                            Plans = new List<SubscriptionSchedulePhaseItemOptions>
                            {
                                new SubscriptionSchedulePhaseItemOptions
                                {
                                    Plan = subscription.Plan.Id,
                                    Quantity = 1
                                },
                            },
                            StartDate = subscription.CurrentPeriodStart,
                            EndDate = subscription.CurrentPeriodEnd
                        },
                        new SubscriptionSchedulePhaseOptions
                        {
                            Plans = new List<SubscriptionSchedulePhaseItemOptions>
                            {
                                new SubscriptionSchedulePhaseItemOptions
                                {
                                    Plan = newPlanId,
                                    Quantity = 1
                                }
                            },
                            StartDate = subscription.CurrentPeriodEnd
                        }
                    }
                };

                await _subscriptionScheduleService.UpdateAsync(subscriptionSchedule.Id,
                    updateSubscriptionScheduleOptions);
                return OperationResult.Success();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating subscription schedule");
                return OperationResult.Failure("error during creating subscription schedule");
            }
        }

        public async Task<OperationResult> UpgradePaidTierPlanAsync(string subscriptionId, string newPlanId)
        {
            try
            {
                var subscription = await _subscriptionService.GetAsync(subscriptionId);

                if (subscription.Status != "active")
                {
                    return OperationResult.Failure("Only active plans can be upgraded");
                }

                var options = new SubscriptionScheduleCreateOptions
                {
                    FromSubscription = subscription.Id,
                };

                var subscriptionSchedule = await _subscriptionScheduleService.CreateAsync(options);

                var updateSubscriptionScheduleOptions = new SubscriptionScheduleUpdateOptions
                {
                    Phases = new List<SubscriptionSchedulePhaseOptions>
                    {
                        new SubscriptionSchedulePhaseOptions
                        {
                            Plans = new List<SubscriptionSchedulePhaseItemOptions>
                            {
                                new SubscriptionSchedulePhaseItemOptions
                                {
                                    Plan = subscription.Plan.Id,
                                    Quantity = 1,
                                },
                            },
                            StartDate = subscription.CurrentPeriodStart,
                            EndDate = subscription.CurrentPeriodEnd,
                        },
                        new SubscriptionSchedulePhaseOptions
                        {
                            Plans = new List<SubscriptionSchedulePhaseItemOptions>
                            {
                                new SubscriptionSchedulePhaseItemOptions
                                {
                                    Plan = newPlanId,
                                    Quantity = 1,
                                }
                            },
                            StartDate = subscription.CurrentPeriodEnd,
                        }
                    },
                };

                var result = await _subscriptionScheduleService.UpdateAsync(subscriptionSchedule.Id,
                    updateSubscriptionScheduleOptions);
                return OperationResult.Success("", result);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating subscription schedule");
                return OperationResult.Failure("error during creating subscription schedule");
            }
        }

        public async Task<OperationResult<Subscription>> UpdateSubscriptionProductPlanAsync(string subscriptionId,
            string planId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                return OperationResult<Subscription>.Failure($"{nameof(subscriptionId)} should not be empty");
            }

            if (string.IsNullOrEmpty(planId))
            {
                return OperationResult<Subscription>.Failure($"{nameof(planId)} should not be empty");
            }

            try
            {
                var subscription = await _subscriptionService.GetAsync(subscriptionId);

                var items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Id = subscription.Items.Data[0].Id,
                        Plan = planId,
                    }
                };

                var options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = false,
                    ProrationBehavior = "none",
                    Items = items,
                };

                var result = await _subscriptionService.UpdateAsync(subscriptionId, options);

                return OperationResult<Subscription>.Success(result);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during upgrading subscription");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> UpdatePaymentIntentPaymentMethodAsync(UpdatePaymentMethodViewModel model, string standardAccountId = null)
        {
            if (model == null)
            {
                return OperationResult.Failure(null);
            }

            var validationResult = await _updatePaymentMethodValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult.Failure(validationResult.Errors.ToString());
            }

            var options = new PaymentIntentUpdateOptions
            {
                PaymentMethod = model.PaymentMethodId
            };

            try
            {
                var intent = await _paymentIntentService.UpdateAsync(model.Id, options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null, intent);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during updating payment method");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> CancelProductPlanSubscriptionAsync(string subscriptionId)
        {
            if (subscriptionId == null)
            {
                return OperationResult.Failure($"'{nameof(subscriptionId)}' must be not empty.");
            }

            var cancelOptions = new SubscriptionCancelOptions
            {
                InvoiceNow = false,
                Prorate = false
            };

            try
            {
                await _subscriptionService.CancelAsync(subscriptionId, cancelOptions);
                return OperationResult.Success(null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during cancelling product plan subscription");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> CancelProductPlanSubscriptionScheduleAsync(string subscriptionScheduleId, string standardAccountId = null)
        {
            if (subscriptionScheduleId == null)
            {
                return OperationResult.Failure($"'{nameof(subscriptionScheduleId)}' must be not empty.");
            }

            var options = new SubscriptionScheduleCancelOptions
            {
                InvoiceNow = false,
                Prorate = false
            };

            try
            {
                await _subscriptionScheduleService.CancelAsync(subscriptionScheduleId, options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "cancel product plan subscription");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<SubscriptionSchedule> GetProductPlanSubscriptionScheduleAsync(string subscriptionScheduleId)
        {
            var options = new SubscriptionScheduleGetOptions();
            options.AddExpand("subscription.latest_invoice");
            options.AddExpand("phases");

            return await _subscriptionScheduleService.GetAsync(subscriptionScheduleId, options);
        }

        public async Task<OperationResult<Subscription>> UpdateProductPlanSubscriptionPaymentMethodAsync(
            UpdatePaymentMethodViewModel model, string standardAccountId = null)
        {
            if (model is null)
            {
                return OperationResult<Subscription>.Failure(null);
            }

            var validationResult = await _updatePaymentMethodValidator.ValidateAsync(model);

            if (!validationResult.IsValid)
            {
                return OperationResult<Subscription>.Failure(validationResult.Errors.ToString());
            }

            var options = new SubscriptionUpdateOptions
            {
                DefaultPaymentMethod = model.PaymentMethodId,
                ProrationBehavior = "none",
            };

            options.AddExpand("latest_invoice.payment_intent");

            try
            {
                var planSubscription = await _subscriptionService.UpdateAsync(model.Id, options, GetStandardAccountRequestOption(standardAccountId));

                return OperationResult<Subscription>.Success(null, planSubscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during updating product plan subscription");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> UpdateProductPlanSubscriptionSchedulePaymentMethodAsync(
            string subscriptionScheduleId, string planId, string paymentMethod, long iterations)
        {
            var options = new SubscriptionScheduleUpdateOptions
            {
                Phases = new List<SubscriptionSchedulePhaseOptions>
                {
                    new SubscriptionSchedulePhaseOptions
                    {
                        Plans = new List<SubscriptionSchedulePhaseItemOptions>
                        {
                            new SubscriptionSchedulePhaseItemOptions
                            {
                                Plan = planId
                            }
                        },
                        DefaultPaymentMethod = paymentMethod,
                        Iterations = iterations
                    }
                }
            };
            options.AddExpand("subscription.latest_invoice");

            try
            {
                var schedule = await _subscriptionScheduleService.UpdateAsync(subscriptionScheduleId, options);
                return OperationResult.Success(null, schedule.Subscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during updating product plan subscription schedule");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> CancelPaymentIntentAsync(string id, string standardAccountId = null)
        {
            if (id == null)
            {
                return OperationResult.Failure($"'{nameof(id)}' must be not empty.");
            }

            try
            {
                await _paymentIntentService.CancelAsync(id, options: null, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during cancelling payment intent");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<Subscription>> CreateTrialSubscription(TrialSubscriptionViewModel model)
        {
            var options = new SubscriptionCreateOptions
            {
                Customer = model.CustomerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Plan = model.StripeSubscriptionPlanId,
                    },
                },
                Metadata = new Dictionary<string, string>
                {
                    { Constants.Stripe.MetadataKeys.ContributionId, model.ContributionId }
                },
                TrialPeriodDays = 730
            };

            try
            {
                var subscription = await _subscriptionService.CreateAsync(options);
                _memoryCache.Set("subscription_" + subscription.Id, subscription, TimeSpan.FromDays(2));
                return OperationResult<Subscription>.Success(subscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating trial subscription");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }
        
        public async Task<OperationResult<Subscription>> CreateTrialSubscription(ProduceTrialSubscriptionViewModel model, string contributionCurrency)
        {
            var options = new SubscriptionCreateOptions()
            {
                Customer = model.CustomerId,
                Items = new List<SubscriptionItemOptions>()
                { 
                    new SubscriptionItemOptions()
                    {
                        PriceData = new SubscriptionItemPriceDataOptions()
                        {
                            Currency = string.IsNullOrWhiteSpace(contributionCurrency)? "usd" : contributionCurrency,
                            UnitAmount = 1,
                            Product = model.ProductId,
                            Recurring = new SubscriptionItemPriceDataRecurringOptions()
                            {
                                Interval = "month"
                            }
                        }
                    },
                },
                Metadata = new Dictionary<string, string>()
                {
                    { Constants.Stripe.MetadataKeys.ContributionId, model.ContributionId },
                },
                TrialPeriodDays = 730,
            };

            try
            {
                var subscription = await _subscriptionService.CreateAsync(options);
                _memoryCache.Set("subscription_" + subscription.Id, subscription, TimeSpan.FromDays(2));
                return OperationResult<Subscription>.Success(subscription);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creating trial subscription");
                return OperationResult<Subscription>.Failure(ex.Message);
            }
        }
        
        public async Task<OperationResult<string>> CreateCheckoutSessionToUpdatePaymentMethod(string stripeCustomerId)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                    "card"
                },
                Customer = stripeCustomerId,
                SetupIntentData = new SessionSetupIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_id", stripeCustomerId }
                    }
                },
                Mode = "setup",
                SuccessUrl = $"{_sessionBillingUrl}?success=true&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_sessionBillingUrl}?success=false",
            };

            var session = await _sessionService.CreateAsync(options);
            return OperationResult<string>.Success(session.Id);
        }

        public async Task<OperationResult<Stripe.Checkout.Session>> CreateSubscriptionCheckoutSession(CreateCheckoutSessionModel model)
        {
            //TODO: remove validation duplication
            if (model.ContributionId is null)
            {
                return OperationResult<Stripe.Checkout.Session>.Failure("contribution Id should not be null");
            }

            if (model.StripeCustomerId is null)
            {
                return OperationResult<Stripe.Checkout.Session>.Failure("stripe customer id should not be null");
            }

            if (model.PriceId is null)
            {
                return OperationResult<Stripe.Checkout.Session>.Failure("price should not be null");
            }

            if (model.paymentType == PaymentTypes.Advance && !model.IsStandardAccount)
            {
                return OperationResult<Stripe.Checkout.Session>.Failure("only standadrd account user can use advance pay");
            }
            //if(!string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full") return OperationResult<Stripe.Checkout.Session>.Failure("Subscription/Split Payments is disabled for beta users");

            SessionCreateOptions options = null;
            options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                    "card"
                },
                LineItems = new List<SessionLineItemOptions>
                {
                new SessionLineItemOptions
                {
                    Price = model.PriceId,
                    Quantity = 1,

                },
                },
                Customer = model.StripeCustomerId,
                Mode = "subscription",
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = model.GetMetadata(),

                },

                Metadata = model.GetMetadata(),
                SuccessUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=success",
                CancelUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=failed",
            }; 

            if (!(model.CouponId is null))
            {
                options.AddExtraParam("discounts[][coupon]", model.CouponId);
            }
                     
            if (model.paymentType == PaymentTypes.Advance && model.IsStandardAccount)
            {
                if (model.TaxType != TaxTypes.No)
                {
                    options.AddExtraParam("automatic_tax[enabled]", true);
                    options.AddExtraParam("customer_update[shipping]", "auto");
                    options.AddExtraParam("customer_update[address]", "auto"); 
                }
            }
            else if (!string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full")
            {
                //hadnle the on_behalf_of case
                options.AddExtraParam("subscription_data[on_behalf_of]", model.ConnectedStripeAccountId);
                options.AddExtraParam("subscription_data[transfer_data][destination]", model.ConnectedStripeAccountId);

                var amountToTransfer = (decimal)(model.BillingInfo.BillingPlanGrossCost - model.BillingInfo.BillingPlanTransferCost) / model.BillingInfo.BillingPlanGrossCost * 100;
                var transfer_amount_percent = Math.Round(100 - amountToTransfer, 2);
                //check if trsanfer amount at stripe is decreasing due to round off
                if ((model.BillingInfo.BillingPlanGrossCost * transfer_amount_percent) / 100m < model.BillingInfo.BillingPlanTransferCost)
                    transfer_amount_percent += 0.01m;
            
                options.AddExtraParam("subscription_data[transfer_data][amount_percent]", transfer_amount_percent);
            }
            
            try
            {
                string standardAccountId = string.Empty;
                if (model.paymentType == PaymentTypes.Advance && model.IsStandardAccount) standardAccountId = model.StripeStandardAccountId;

                var session = await _sessionService.CreateAsync(options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult<Stripe.Checkout.Session>.Success(session);
            }
            catch (Exception ex)
            {
                await _notificationService.SendPurchaseFailNotifcationToCoach(model.CoachEmail, model.ClientFirstName, model.ClientLastName, model.ClientEmail, ex.Message, model.ContributionTitle);
                _logger.LogError(ex, "error during creation checkout session");
                return OperationResult<Stripe.Checkout.Session>.Failure("error during creation checkout session");
            }
        }

        public async Task<OperationResult<Session>> CreateCheckoutSessionSinglePayment(CreateCheckoutSessionModel model)
        {
            //TODO: remove validation duplication
            if (model.ContributionId is null)
            {
                return OperationResult<Session>.Failure("contribution Id should not be null");
            }

            if (model.StripeCustomerId is null)
            {
                return OperationResult<Session>.Failure("stripe customer id should not be null");
            }

            if (model.PriceId is null)
            {
                return OperationResult<Session>.Failure("price should not be null");
            }
            SessionCreateOptions options = null;
            if ((!string.IsNullOrEmpty(model.ServiceAgreementType) && model.ServiceAgreementType == "full") || (model.paymentType == PaymentTypes.Advance && model.IsStandardAccount))
            {
                decimal? totalCost = model.ProductCost, stripeFeesForCoherePlatform;


                if (model.DiscountPercent != null && model.PaymentOption != PaymentOptions.PerSession)
                {
                    totalCost = (model.ProductCost - (model.ProductCost * model.DiscountPercent / 100));
                }
                stripeFeesForCoherePlatform = model.TotalChargedCost - totalCost;
                if (stripeFeesForCoherePlatform == 0)
                {
                    if (model.CoachPaysStripeFee)
                        stripeFeesForCoherePlatform = ((totalCost / 100) * model.StripeFee) + model.FixedStripeAmount;
                    else
                        stripeFeesForCoherePlatform = (totalCost + model.FixedStripeAmount) / (1 - model.StripeFee / 100) - totalCost;
                }
                decimal fee = stripeFeesForCoherePlatform ?? 0;

                var transferAmountWithoutCoupon = Convert.ToInt64(Math.Round((model.TotalChargedCost - fee), 2) * 100);
                long totalAmountForTransfer = transferAmountWithoutCoupon;

                decimal couponDiscountInPercentage = 1m;
                if (!string.IsNullOrEmpty(model.CouponId))
                {
                    couponDiscountInPercentage = (100m - (decimal)model.CouponPerecent) / 100;
                }

                if (model.CouponId != null && model.CouponPerecent != null)
                {
                    if (model.CoachPaysStripeFee == true)
                    {
                        decimal amountWithoutDeduction = Math.Round(((decimal)transferAmountWithoutCoupon), 2);
                        var totalAmount_WithCoupon = model.TotalChargedCost * couponDiscountInPercentage;
                        var fee_WithCoupon = (totalAmount_WithCoupon * (model.StripeFee / 100)) + model.FixedStripeAmount;
                        fee_WithCoupon = Math.Round(fee_WithCoupon, 2, MidpointRounding.AwayFromZero);
                        //var percentToAmount = Convert.ToInt64(Math.Round((amountWithoutDeduction - amountWithoutDeduction / 100 * model.CouponPerecent ?? 1), 2) * 100);
                        var percentToAmount = (totalAmount_WithCoupon - fee_WithCoupon) * 100;
                        totalAmountForTransfer = (long)percentToAmount;
                    }
                    else
                    {
                        decimal amountWithoutDeduction = Math.Round(((decimal)transferAmountWithoutCoupon / 100), 2);
                        var percentToAmount = Convert.ToInt64(Math.Round((amountWithoutDeduction - amountWithoutDeduction / 100 * model.CouponPerecent ?? 1), 2) * 100);
                        totalAmountForTransfer = percentToAmount;
                    }
                }

                options = new SessionCreateOptions()
                {
                    PaymentMethodTypes = new List<string>
                {
                    "card"
                },
                    Customer = model.StripeCustomerId,
                    LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = model.PriceId,
                        Quantity = 1,

                    }
                },

                    Mode = "payment",
                    SuccessUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=success",
                    CancelUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=failed"
                };

                if (model.paymentType == PaymentTypes.Advance && model.IsStandardAccount)
                {
                    options.PaymentIntentData = new SessionPaymentIntentDataOptions()
                    {
                        Metadata = model.GetMetadata(),
                    };

                    if (model.TaxType != TaxTypes.No)
                    {
                        options.AddExtraParam("automatic_tax[enabled]", true);
                        options.AddExtraParam("customer_update[shipping]", "auto");
                        options.AddExtraParam("customer_update[address]", "auto");
                    } 
                }
                else
                {
                    //on_behalf_of
                    options.PaymentIntentData = new SessionPaymentIntentDataOptions()
                    {
                        Metadata = model.GetMetadata(),
                        OnBehalfOf = model.ConnectedStripeAccountId,
                        TransferData = new SessionPaymentIntentTransferDataOptions()
                        {
                            Amount = totalAmountForTransfer,
                            Destination = model.ConnectedStripeAccountId
                        }
                    };
                }
            }
            else
            {
                options = new SessionCreateOptions()
                {
                    PaymentMethodTypes = new List<string>
                {
                    "card"
                },
                    Customer = model.StripeCustomerId,
                    LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = model.PriceId,
                        Quantity = 1,

                    }
                },

                    Mode = "payment",
                    SuccessUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=success",
                    CancelUrl = $"{_contributionViewUrl}{model.ContributionId}/about?payment=failed",
                    PaymentIntentData = new SessionPaymentIntentDataOptions()
                    {
                        Metadata = model.GetMetadata(),
                    },

                };
            }
            
            
            if (!(model.CouponId is null))
            {
                options.AddExtraParam("discounts[][coupon]", model.CouponId);
            }

            try
            {
                string standardAccountId = string.Empty;
                if (model.paymentType == PaymentTypes.Advance && model.IsStandardAccount) standardAccountId = model.StripeStandardAccountId;

                var session = await _sessionService.CreateAsync(options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult<Session>.Success(session);
            }
            catch (Exception ex)
            {
                await _notificationService.SendPurchaseFailNotifcationToCoach(model.CoachEmail, model.ClientFirstName, model.ClientLastName, model.ClientEmail, ex.Message, model.ContributionTitle);
                _logger.LogError(ex, "error during creation checkout session");
                return OperationResult<Session>.Failure("error during creation checkout session");
            }
        }

        public RequestOptions GetStandardAccountRequestOption(string standardAccountId)
        {
            if (string.IsNullOrEmpty(standardAccountId))
            {
                return null;
            }
            return new RequestOptions { StripeAccount = standardAccountId };
        }

        public async Task<OperationResult<string>> CreateCheckoutSessionSubscription(
            string stripeCustomerId, string priceId, PaidTierOption paidTierOption, string coachStripeAccount)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                    "card"
                },
                Customer = stripeCustomerId,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                },
                Mode = "subscription",
                SuccessUrl = $"{_coachSessionBillingUrl}?success=true&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_coachSessionBillingUrl}?success=false",
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { Constants.Stripe.MetadataKeys.PaidTierId, paidTierOption.Id }
                    }
                }
            };
            options.AddExtraParam("allow_promotion_codes", "true");

            //TODO:ONCE HOOK IS TESTED FOR OLD TRANSFER METHOD WE WILL UNCOMMENT THIS SO THAT BETA  USER CAN HAVE THE REFERAL FUNCTIONALITY

            if (!string.IsNullOrWhiteSpace(coachStripeAccount))
            {
                options.AddExtraParam("subscription_data[on_behalf_of]", coachStripeAccount);
                options.AddExtraParam("subscription_data[transfer_data][amount_percent]", 30);
                options.AddExtraParam("subscription_data[transfer_data][destination]", coachStripeAccount);
            }

            try
            {
                var session = await _sessionService.CreateAsync(options);
                return OperationResult<string>.Success(session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during creation checkout session");
                return OperationResult<string>.Failure("error during creation checkout session");
            }
        }

        public async Task<OperationResult<string>> CreateCustomerPortalLink(string stripeCustomerId)
        {
            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = stripeCustomerId,
                ReturnUrl = _sessionBillingUrl
            };
            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);
            return OperationResult<string>.Success(session.Url);
        }

        public async Task<OperationResult<StripeList<ApplicationFee>>> GetApplicationFeesAsync(ApplicationFeeListOptions options)
		{
            StripeList<ApplicationFee> applicationFees = await _applicationFeeService.ListAsync(
			    options
		    );
            return OperationResult<StripeList<ApplicationFee>>.Success(applicationFees);
        }

		public async Task<OperationResult> AgreeToStripeAgreement(string stripeConnectedAccountId, string ipAddress)
		{
		    var options = new AccountUpdateOptions
			{
				TosAcceptance = new AccountTosAcceptanceOptions
				{
					Date = DateTime.UtcNow,
					Ip = ipAddress,
				},
			};
			var service = new AccountService();
			try
			{
				var updateResult = await service.UpdateAsync(stripeConnectedAccountId, options);
				if (updateResult?.Id == stripeConnectedAccountId)
				{
					return OperationResult.Success();
				}
			}
			catch
			{

			}
			return OperationResult.Failure("Updating stripe account failed");
		}
    }
}