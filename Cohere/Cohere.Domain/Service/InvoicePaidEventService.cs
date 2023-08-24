using System;
using System.Collections.Generic;
using System.Linq;
using Cohere.Domain.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.Enums.Account;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Microsoft.Extensions.Logging;
using Stripe;
using Account = Cohere.Entity.Entities.Account;
using StripeEvent = Stripe.Event;

namespace Cohere.Domain.Service
{
    public class InvoicePaidEventService : IInvoicePaidEventService
    {
        private readonly IStripeService _stripeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;
        private readonly IPricingCalculationService _pricingCalculationService;
        private readonly IPaidTierPurchaseService _paidTierPurchaseService;
        private readonly ContributionPurchaseService _contributionPurchaseService;
        private readonly ISynchronizePurchaseUpdateService _synchronizePurchaseUpdateService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly INotificationService _notificationService;
        private readonly IAccountManager _accountManager;
        private readonly IJobScheduler _jobScheduler;
        private readonly IFCMService _fcmService;
        private readonly ICommonService _commonService;
        private readonly ILogger<InvoicePaidEventService> _logger;
        public InvoicePaidEventService(
            IStripeService stripeService,
            IUnitOfWork unitOfWork,
            IPricingCalculationService pricingCalculationService,
            ContributionPurchaseService contributionPurchaseService,
            ISynchronizePurchaseUpdateService synchronizePurchaseUpdateService,
            IPaidTierPurchaseService paidTierPurchaseService,
            IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService,
            IActiveCampaignService activeCampaignService,
            INotificationService notificationService,
            IAccountManager accountManager,
            IJobScheduler jobScheduler,
            IFCMService fcmService,
            ICommonService commonService,
            ILogger<InvoicePaidEventService> logger)
        {
            _stripeService = stripeService;
            _unitOfWork = unitOfWork;
            _pricingCalculationService = pricingCalculationService;
            _contributionPurchaseService = contributionPurchaseService;
            _synchronizePurchaseUpdateService = synchronizePurchaseUpdateService;
            _paidTierPurchaseService = paidTierPurchaseService;
            _paidTiersService = paidTiersService;
            _activeCampaignService = activeCampaignService;
            _notificationService = notificationService;
            _accountManager = accountManager;
            _jobScheduler = jobScheduler;
            _fcmService = fcmService;
            _commonService = commonService;
            _logger = logger; 
        }

        public OperationResult HandleInvoicePaidEvent(Event @event, bool forStandardAccount)
        {
            try
            {
                if (!(@event.Data.Object is Invoice invoice))
                {
                    _logger.Log(LogLevel.Error, $" The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type at {DateTime.UtcNow}.");
                    return OperationResult.Failure(
                        $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
                }

                string stripeStandardAccountId = string.Empty;
                if (forStandardAccount) stripeStandardAccountId = @event.Account;

                var isSubscription = invoice.SubscriptionId != null;
                var fullInvoice = _stripeService.GetInvoiceAsync(invoice.Id, stripeStandardAccountId).GetAwaiter().GetResult();
                if (isSubscription && fullInvoice.Subscription.Metadata.TryGetValue(
                    Constants.Stripe.MetadataKeys.PaidTierId, out var paidTierId))
                {
                    return HandlePairTierInvoicePaidStripeEvent(@event, paidTierId, fullInvoice);
                }

                if (isSubscription && fullInvoice.Subscription.Metadata.TryGetValue(
                    Constants.Stripe.MetadataKeys.ContributionId, out var contributionId))
                {
                    return HandleContributionInvoicePaidStripeEvent(@event, contributionId, fullInvoice, stripeStandardAccountId);
                }

                //for payment of contribution not as subscription

                if (fullInvoice.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.PaymentOption, out var paymentOption) &&
                    fullInvoice.Metadata.TryGetValue(Constants.Stripe.MetadataKeys.ContributionId, out var contributionID))
                {

                    if (paymentOption == PaymentOptions.EntireCourse.ToString())
                    {
                        fullInvoice.PaymentIntent.Metadata = new Dictionary<string, string>
                        {
                            {Constants.Stripe.MetadataKeys.PaymentOption, paymentOption},
                            {Constants.Stripe.MetadataKeys.ContributionId, contributionID }
                        };

                        _contributionPurchaseService.HandlePaymentIntentStripeEvent(@event, forStandardAccount, isPaidByInvoice: true, fullInvoice);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandleInvoicePaidEvent at {DateTime.UtcNow}.");
            }
            return OperationResult.Success();
        }

        private OperationResult HandlePairTierInvoicePaidStripeEvent(
            Event @event,
            string paidTierId,
            Invoice fullInvoice)
        {
            try
            {
                if (@event.Data.Object is Invoice)
                {
                    var customerStripeId = fullInvoice.CustomerId;

                    var client = _unitOfWork.GetRepositoryAsync<User>()
                        .GetOne(u => u.CustomerStripeAccountId == customerStripeId).GetAwaiter().GetResult();

                    var paidTierOption = _unitOfWork.GetRepositoryAsync<PaidTierOption>()
                        .GetOne(p => p.Id == paidTierId).GetAwaiter().GetResult();

                    var coachAccount = _unitOfWork.GetRepositoryAsync<Account>()
                        .GetOne(a => a.Id == client.AccountId).GetAwaiter().GetResult();

                    // coach completed the purchase, set signup type (for marketing purposes) to none
                    if (coachAccount.SignupType != SignupTypes.NONE)
                    {
                        coachAccount.SignupType = SignupTypes.NONE;
                        var updatedAccount = _unitOfWork.GetRepositoryAsync<Account>().Update(coachAccount.Id, coachAccount)
                            .GetAwaiter().GetResult();
                        if (updatedAccount != null)
                        {
                            coachAccount = updatedAccount;
                        }

                        // since confirmation email was not sent to this sign up type user, send it now
                        if (!coachAccount.IsEmailConfirmed)
                        {
                            _accountManager.RequestEmailConfirmationAsync(coachAccount.Id).GetAwaiter().GetResult();
                        }
                    }

                    var purchase = _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                    .GetOne(pt => pt.ClientId == client.Id && pt.SubscriptionId == fullInvoice.Subscription.Id)
                    .GetAwaiter().GetResult();

                    purchase ??= new PaidTierPurchase
                    {
                        ClientId = client.Id,
                        SubscriptionId = fullInvoice.Subscription.Id,
                        IsFirstPaymentHandled = true,
                        Payments = new List<PaidTierPurchasePayment>()
                    };

                    var paidTierPeriod = default(PaidTierOptionPeriods);

                    if (fullInvoice.Subscription.Plan.Id == paidTierOption.PaidTierInfo.ProductMonthlyPlanId)
                    {
                        paidTierPeriod = PaidTierOptionPeriods.Monthly;
                    }
                    else if (fullInvoice.Subscription.Plan.Id == paidTierOption.PaidTierInfo.ProductAnnuallyPlanId)
                    {
                        paidTierPeriod = PaidTierOptionPeriods.Annually;
                    }
                    else if (fullInvoice.Subscription.Plan.Id == paidTierOption.PaidTierInfo.ProductSixMonthPlanId)
                    {
                        paidTierPeriod = PaidTierOptionPeriods.EverySixMonth;
                    }

                    long purchaseNetAmount;
                    long grossAmmount;
                    // deal with 100% coupon code
                    if (!(fullInvoice?.Charge?.BalanceTransaction?.Net > 0) && fullInvoice.Total == 0)
                    {
                        purchaseNetAmount = fullInvoice.Total;
                        grossAmmount = fullInvoice.Subtotal;
                    }
                    else
                    {
                        purchaseNetAmount = fullInvoice.Charge.BalanceTransaction.Net;
                        grossAmmount = fullInvoice.Charge.BalanceTransaction.Amount;
                    }

                    var serviceProviderIncome = purchaseNetAmount / _stripeService.SmallestCurrencyUnit;

                    Models.Affiliate.CreateMoneyTransferResult transfer = null;
                    if (fullInvoice.Charge != null && fullInvoice.TransferData == null)
                    {
                        var moneyTransferResult = _paidTierPurchaseService.CreateTransfers(
                            coachAccount,
                            fullInvoice.Charge);

                        if (moneyTransferResult.Failed)
                        {
                            //return moneyTransferResult;
                            transfer = null;
                        }
                        else
                        {
                            transfer = moneyTransferResult.Payload;
                        }

                    }

                    var payment = new PaidTierPurchasePayment
                    {
                        TransactionId = fullInvoice?.PaymentIntent?.Id ?? fullInvoice?.Id,
                        PaymentOption = paidTierPeriod,
                        DateTimeCharged = fullInvoice?.PaymentIntent != null ?
                            fullInvoice.PaymentIntent.Created :
                            fullInvoice.Created,
                        PaymentStatus = fullInvoice.Subscription.LatestInvoice.Status.ToPaymentStatusEnum(),
                        PeriodEnds = fullInvoice.Subscription.CurrentPeriodEnd,
                        PurchaseAmount = purchaseNetAmount / _stripeService.SmallestCurrencyUnit,
                        GrossPurchaseAmount = grossAmmount / _stripeService.SmallestCurrencyUnit,
                        TransferAmount = serviceProviderIncome,
                    };

                    if (transfer?.AffiliateTransfer != null)
                    {
                        payment.AffiliateRevenueTransfer = new AffiliateRevenueTransfer
                        {
                            Amount = transfer.AffiliateTransfer.Amount / _stripeService.SmallestCurrencyUnit
                        };
                    }
                    else if (fullInvoice.TransferData != null)
                    {
                        payment.AffiliateRevenueTransfer = new AffiliateRevenueTransfer
                        {
                            Amount = (fullInvoice.TransferData.Amount / _stripeService.SmallestCurrencyUnit) ?? 0
                        };
                    }

                    purchase.Payments.Add(payment);

                    if (string.IsNullOrEmpty(purchase.Id))
                    {
                        _unitOfWork.GetRepositoryAsync<PaidTierPurchase>().Insert(purchase);

                        //send email for new paidtier signup
                        var subscriptionResult = _commonService.GetProductPlanSubscriptionAsync(purchase.SubscriptionId).GetAwaiter().GetResult();
                        var subscription = subscriptionResult.Payload;
                        var currentPaidtierPlan = _commonService.GetPaidTierByPlanId(subscription.Plan.Id).GetAwaiter().GetResult();
                        var billingFrequency = currentPaidtierPlan.PaidTierInfo.GetStatus(subscription.Plan.Id);
                        var customerName = $"{client.FirstName} {client.LastName}";
                        var nextRenewelDate = _commonService.GetNextRenewelDateOfPlan(billingFrequency, purchase.CreateTime);
                        _notificationService.SendNotificationForNewSignupOfPaidtierAccount(customerName, coachAccount.Email, billingFrequency.ToString(),
                            currentPaidtierPlan.DisplayName, purchase.CreateTime, nextRenewelDate);

                        //Insert Referral Transfer Data info in Db for beta coaches
                        if (transfer?.AffiliateTransfer != null)
                        {
                            decimal referralAmount = (transfer.AffiliateTransfer.Amount / _stripeService.SmallestCurrencyUnit);
                            if (referralAmount > 0)
                            {
                                string referralCoachStripeId = transfer.AffiliateTransfer.DestinationId;
                                User referralUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.ConnectedStripeAccountId == referralCoachStripeId).GetAwaiter().GetResult();
                                ReferralsInfo referralInfo = new ReferralsInfo()
                                {
                                    ReferralAmount = referralAmount,
                                    ReferralUserId = referralUser.Id,
                                    ReferredUserId = client.Id,
                                    TransferTime = DateTime.UtcNow
                                };
                                _unitOfWork.GetRepositoryAsync<ReferralsInfo>().Insert(referralInfo);
                            }
                        }
                        else if (fullInvoice.TransferData != null)
                        {
                            decimal referralAmount = (fullInvoice.TransferData.Amount / _stripeService.SmallestCurrencyUnit) ?? 0;
                            if (referralAmount > 0)
                            {
                                string referralCoachStripeId = fullInvoice.TransferData.DestinationId;
                                User referralUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.ConnectedStripeAccountId == referralCoachStripeId).GetAwaiter().GetResult();
                                ReferralsInfo referralInfo = new ReferralsInfo()
                                {
                                    ReferralAmount = referralAmount,
                                    ReferralUserId = referralUser.Id,
                                    ReferredUserId = client.Id,
                                    TransferTime = DateTime.UtcNow
                                };
                                _unitOfWork.GetRepositoryAsync<ReferralsInfo>().Insert(referralInfo);
                            }
                        }
                    }
                    else
                    {
                        _synchronizePurchaseUpdateService.Sync(purchase);
                    }

                    var activeCampaignDeal = new ActiveCampaignDeal()
                    {
                        Value = grossAmmount.ToString()

                    };
                    string paidTearOption = _activeCampaignService.PaidTearOptionToActiveCampaignDealCustomFieldValue(paidTierOption, paidTierPeriod);
                    ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                    {
                        CohereAccountId = client.AccountId,
                        PaidTier = paidTearOption,
                        PaidTierCreditCardStatus = EnumHelper<CreditCardStatus>.GetDisplayValue(CreditCardStatus.Normal),

                    };
                    _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);

                    return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $" {ex.Message} in HandlePairTierInvoicePaidStripeEvent at {DateTime.UtcNow}.");
            }
            _logger.Log(LogLevel.Error, $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type at {DateTime.UtcNow}.");
            return OperationResult.Failure(
                $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
        }

        private OperationResult HandleContributionInvoicePaidStripeEvent(
            Event @event,
            string contributionId,
            Invoice fullInvoice,
            string standardAccountId)
        {
            if (!(@event.Data.Object is Invoice))
                return OperationResult.Failure(
                    $"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(Invoice).FullName}' type");
            
            Models.Affiliate.CreateMoneyTransferResult transfer = null;

            var customerStripeId = fullInvoice.CustomerId;

            var client = _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(e => e.CustomerStripeAccountId == customerStripeId).GetAwaiter().GetResult();
            
            // Handling Exceptions
            if (client == null) return OperationResult.Failure($"Can't find Client with CustomerStripeId: ${customerStripeId}");

            var contribution = _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .GetOne(e => e.Id == contributionId).GetAwaiter().GetResult();
            // Handling Exceptions
            if (contribution == null) return OperationResult.Failure($"Can't find Selected Contribution with Id: {contributionId}");

            var contributorUser = _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(e => e.Id == contribution.UserId).GetAwaiter().GetResult();

            var contributorAccount = _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(e => e.Id == contributorUser.AccountId).GetAwaiter().GetResult();

            var purchase = _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(e =>
                e.ContributionId == contribution.Id && e.ClientId == client.Id).GetAwaiter().GetResult();

            CheckIfLastSplitPaymentAndHandle(fullInvoice, contribution, standardAccountId);

            var isTrial = fullInvoice.Subscription.TrialEnd > DateTime.UtcNow;

            if (!isTrial)
            {
                for (int i = 0; i < 100; i++)
                {
                    if (purchase == null)
                    {
                        System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(100));
                        purchase = _unitOfWork.GetRepositoryAsync<Purchase>()
                        .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == client.Id).GetAwaiter()
                        .GetResult();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // first payment is being handled on the ContributionPurchaseService - HandlePaymentIntentStripeEvent
            if (purchase?.Payments?
                .Where(p => p.PaymentStatus == Entity.Enums.Payments.PaymentStatus.Succeeded || p.PaymentStatus == Entity.Enums.Payments.PaymentStatus.Paid)
                .Count() == 1 && fullInvoice?.Charge?.BalanceTransaction?.Amount != null)
            {

                var paidAmount = (fullInvoice.Charge.BalanceTransaction.Amount / _stripeService.SmallestCurrencyUnit).ToString();
                var clientAccount = _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(e => e.Id == client.AccountId).GetAwaiter().GetResult();

                _notificationService
                .SendClientEnrolledNotification(contribution, client.FirstName, clientAccount.Email, paidAmount)
                .GetAwaiter()
                .GetResult();

                if(contribution.IsInvoiced)
                {
                    //send notification of invoice paid only for invoiced contribution.
                    _notificationService
                    .SendInvoicePaidEmailToCoach(contributorUser.AccountId, fullInvoice.CustomerEmail, fullInvoice.Number, contribution.Title)
                    .GetAwaiter().GetResult();
                }


                if (string.IsNullOrEmpty(purchase.SubscriptionId))
                {
                    purchase.SubscriptionId = fullInvoice.SubscriptionId;
                    _unitOfWork.GetRepositoryAsync<Purchase>().Update(purchase.Id, purchase).GetAwaiter().GetResult();
                }

                return OperationResult.Success();
            }

            purchase ??= new Purchase()
            {
                ContributionId = contribution.Id,
                ContributorId = contribution.UserId,
                ClientId = client.Id,
                ContributionType = contribution.Type,
                IsFirstPaymentHandeled = false,
                PaymentType = contribution.PaymentType.ToString(),
                TaxType = contribution.PaymentType == PaymentTypes.Advance ? contribution.TaxType.ToString() : string.Empty
            };

            purchase.SubscriptionId = fullInvoice.Subscription.Id;

            PurchasePayment payment = null;

            PaymentOptions paymentOption;
            if (contribution?.PaymentInfo?.MembershipInfo?.PaymentOptionsMap != null)
            {
                var paymentOptionByProductPlan =
                    contribution.PaymentInfo.MembershipInfo.PaymentOptionsMap;

                if (!paymentOptionByProductPlan.TryGetValue(
                fullInvoice.Subscription.Plan.Id,
                out paymentOption))
                {
                    return OperationResult.Failure("Payment Option can't be found");
                }
            }
            else if (contribution?.PaymentInfo?.BillingPlanInfo != null)
            {
                if (contribution.PaymentInfo.BillingPlanInfo.ProductBillingPlanId == fullInvoice.Subscription.Plan.Id)
                {
                    paymentOption = PaymentOptions.SplitPayments;
                }
                else
                {
                    return OperationResult.Failure("Payment Option can't be found");
                }
            }
            else
            {
                return OperationResult.Failure("Payment Option can't be found");
            }


            var purchaseNetAmount = fullInvoice?.Charge?.BalanceTransaction?.Net ?? fullInvoice.AmountPaid;
            var purchaseGrossAmount = fullInvoice?.Charge?.BalanceTransaction?.Amount ?? fullInvoice.AmountPaid;

            var currentPaidTierViewModel = _paidTiersService.GetCurrentPaidTier(contributorUser.AccountId)
                .GetAwaiter().GetResult();

            var serviceProviderIncomeInCents =
                _pricingCalculationService.CalculateServiceProviderIncomeFromNetPurchaseAmountAsLong(
                    purchaseNetAmount, currentPaidTierViewModel.PaidTierOption.NormalizedFee,
                    contribution.PaymentInfo.CoachPaysStripeFee, purchaseGrossAmount);

            if (contribution.PaymentType != PaymentTypes.Advance)
            {
                var moneyTransferResult = _contributionPurchaseService.CreateTransfers(
                        contributorUser.ConnectedStripeAccountId,
                        contributorAccount,
                        fullInvoice.Charge,
                        currentPaidTierViewModel.PaidTierOption,
                        serviceProviderIncomeInCents,
                        contribution.PaymentInfo.CoachPaysStripeFee ? purchaseGrossAmount : purchaseNetAmount);

                if (moneyTransferResult.Failed)
                {

                    transfer = null;
                }
                else
                    transfer = moneyTransferResult.Payload;

                transfer = moneyTransferResult.Payload; 
            }

            payment = new PurchasePayment()
            {
                TransactionId = fullInvoice.PaymentIntentId,
                DateTimeCharged = fullInvoice.PaymentIntent.Created,
                PaymentOption = paymentOption,
                PaymentStatus = fullInvoice.PaymentIntent.Status.ToPaymentStatusEnum(),
                PeriodEnds = fullInvoice.Subscription.CurrentPeriodEnd,
                PurchaseAmount = purchaseNetAmount / _stripeService.SmallestCurrencyUnit,
                GrossPurchaseAmount = fullInvoice.Charge.BalanceTransaction.Amount /
                                        _stripeService.SmallestCurrencyUnit,
                ProcessingFee = fullInvoice.Charge.BalanceTransaction.Fee /
                                _stripeService.SmallestCurrencyUnit,
                TransferAmount = serviceProviderIncomeInCents / _stripeService.SmallestCurrencyUnit,
                IsInEscrow = !contribution.InvitationOnly,
                PurchaseCurrency = contribution.DefaultCurrency,
                Currency = contribution.DefaultCurrency
            };

            if (transfer?.AffiliateTransfer != null && fullInvoice.TransferData == null)
            {
                var paymentsWithAffiliateRevenue = purchase.Payments;

                payment.AffiliateRevenueTransfer = new AffiliateRevenueTransfer()
                {
                    Amount = transfer.AffiliateTransfer.Amount / _stripeService.SmallestCurrencyUnit,
                    IsInEscrow =
                        (paymentsWithAffiliateRevenue.All(e => e.IsInEscrow) &&
                            paymentsWithAffiliateRevenue.Any()) || !paymentsWithAffiliateRevenue.Any()
                };
            }

            purchase.SubscriptionId = fullInvoice.SubscriptionId;

            purchase.Payments.Add(payment);


            if (string.IsNullOrEmpty(purchase.Id))
            {
                _unitOfWork.GetRepositoryAsync<Purchase>().Insert(purchase).GetAwaiter().GetResult();

                if (!string.IsNullOrWhiteSpace(contributorAccount.InvitedBy))
                {
                    ReferralsInfo referralInfo = null;
                    string referralCoachStripeId = transfer.AffiliateTransfer.DestinationId;

                    if (transfer?.AffiliateTransfer != null)
                    {
                        decimal referralAmount = (transfer.AffiliateTransfer.Amount / _stripeService.SmallestCurrencyUnit);
                        if (referralAmount > 0)
                        {
                            User referralUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.ConnectedStripeAccountId == referralCoachStripeId).GetAwaiter().GetResult();
                            referralInfo = new ReferralsInfo()
                            {
                                ReferralAmount = referralAmount,
                                ReferralUserId = referralUser.Id,
                                ReferredUserId = client.Id,
                                TransferTime = DateTime.UtcNow
                            };
                        }
                    }
                    else if (fullInvoice.TransferData != null)
                    {
                        decimal referralAmount = (fullInvoice.TransferData.Amount / _stripeService.SmallestCurrencyUnit) ?? 0;
                        if (referralAmount > 0)
                        {
                            User referralUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.ConnectedStripeAccountId == referralCoachStripeId).GetAwaiter().GetResult();
                            referralInfo = new ReferralsInfo()
                            {
                                ReferralAmount = referralAmount,
                                ReferralUserId = referralUser.Id,
                                ReferredUserId = client.Id,
                                TransferTime = DateTime.UtcNow
                            };
                        }
                        if (referralInfo != null)
                            _unitOfWork.GetRepositoryAsync<ReferralsInfo>().Insert(referralInfo);
                    }

                    _contributionPurchaseService.AfterFirstPaymentHandled(contribution, fullInvoice,
                    fullInvoice.PaymentIntent, purchase, payment, null, client);
                }
            }
            else
            {
                _synchronizePurchaseUpdateService.Sync(purchase);
            }           

            if (!isTrial)
            {
                for (int i = 0; i < 1000; i++)
                {
                    if ((contribution is ContributionMembership || contribution is ContributionCommunity) && !purchase.IsFirstPaymentHandeled)
                    {
                        System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(100));
                        purchase = _unitOfWork.GetRepositoryAsync<Purchase>()
                        .GetOne(x => x.ContributionId == contribution.Id && x.ClientId == client.Id).GetAwaiter()
                        .GetResult();
                    }
                }
            }
            if (payment.TransactionId == null && 
                (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free) || contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Trial)
                || payment.PaymentOption == PaymentOptions.Trial || payment.PaymentOption == PaymentOptions.Free
                ))
            {
                AfterSave(contribution, purchase, payment, client);
                //Send Push Notification (Free Contribution)
                try
                {
                    _fcmService.SendFreeGroupContributionJoinPushNotification(contribution.Id, client.Id);
                }
                catch
                {

                }
            }

            if (contribution.IsInvoiced)
            {
                //send notification of invoice paid only for invoiced contribution.
                _notificationService
                .SendInvoicePaidEmailToCoach(contributorUser.AccountId, fullInvoice.CustomerEmail, fullInvoice.Number, contribution.Title)
                .GetAwaiter().GetResult();
            }

            return OperationResult.Success($"Event with ID: '{@event.Id}' has been successfully processed");

        }
        private void AfterSave(
            ContributionBase contribution,
            Purchase clientPurchase,
            PurchasePayment payment,
            User user
            )
        {
            _jobScheduler.EnqueueAdync<IBookIfSingleSessionTimeJob>(
                contribution.Id,
                clientPurchase.Id,
                string.Empty,
                user.AccountId,
                true);
        }

        private OperationResult CheckIfLastSplitPaymentAndHandle(Invoice fullInvoice, ContributionBase contribution, string standardAccountId)
        {
            if (fullInvoice.Subscription.Metadata.TryGetValue(
                Constants.Stripe.MetadataKeys.PaymentOption, out var paymentOption))
            {
                if (paymentOption == PaymentOptions.SplitPayments.ToString())
                {
                    int spliPaymenttNumber = contribution.PaymentInfo.SplitNumbers ?? 0;
                    if (fullInvoice.Subscription.Metadata.TryGetValue(
                        Constants.Stripe.MetadataKeys.SplitNumbers, out string splitPaymentNumberString))
                    {
                        Int32.TryParse(splitPaymentNumberString, out spliPaymenttNumber);
                    }
                    if (spliPaymenttNumber > 0)
                    {
                        var allCustomerInvoiceItems = _stripeService.GetAllInvoiceAsync(fullInvoice.Subscription.CustomerId, standardAccountId).GetAwaiter().GetResult();
                        var allCharges = allCustomerInvoiceItems?.Where(i => i.SubscriptionId == fullInvoice.Subscription.Id);
                        if (allCharges?.Count() >= spliPaymenttNumber)
                        {
                            _stripeService.CancelSubscriptionImmediately(fullInvoice.SubscriptionId, standardAccountId).GetAwaiter().GetResult();
                        }

                    }

                }
            }
            return OperationResult.Success();
        }
    }
}