using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Domain.Extensions;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Stripe;

namespace Cohere.Domain.Models.Payment
{
    public class PurchaseViewModel : BaseDomain
    {
        private readonly PaymentIntentService _paymentIntentService;
        private readonly SubscriptionService _subscriptionService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IContributionRootService _contributionRootService;
        private readonly InvoiceService _invoiceService;
        private readonly IMemoryCache _memoryCache;
        private readonly ICommonService _commonService;

        public PurchaseViewModel(
            PaymentIntentService paymentIntentService,
            SubscriptionService subscriptionService,
            IUnitOfWork unitOfWork,
            IContributionRootService contributionRootService,
            InvoiceService invoiceService,
            IMemoryCache memoryCache,
            ICommonService commonService)
        {
            _paymentIntentService = paymentIntentService;
            _subscriptionService = subscriptionService;
            _unitOfWork = unitOfWork;
            _contributionRootService = contributionRootService;
            _invoiceService = invoiceService;
            _memoryCache = memoryCache;
            _commonService = commonService;
        }

        public string ClientId { get; set; }

        public string ContributorId { get; set; }

        public string ContributionId { get; set; }

        public string SubscriptionId { get; set; }

        public Subscription Subscription { get; set; }

        public bool IsTrialSubscription => Subscription?.TrialEnd > DateTime.UtcNow;
        public int? SplitNumbers { get; set; }

        public PaymentSplitPeriods? SplitPeriod { get; set; }

        public List<PurchasePayment> Payments { get; set; }
        public string PaymentType { get; set; }
        public string TaxType { get; set; }
        public string ContributionType { get; set; }

        public DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase { get; set; }

        public bool HasProcessingPayment => Payments.Exists(x => x.PaymentStatus == PaymentStatus.Processing);

        public bool HasUnconfirmedPayment => HasUnconfirmedPaymentOption();

        public bool HasActiveSubscription =>
            Subscription?.Status == "active" || Subscription?.Status == "trialing";

        public bool HasAccessToContribution =>
            ContributionType == nameof(ContributionMembership) || ContributionType == nameof(ContributionCommunity) || RecentPaymentOption == PaymentOptions.Trial
                ? HasActiveSubscription
                : HasSucceededPayment;

        public bool HasUnconfirmedPaymentOption(PaymentOptions? option = null) => Payments.Exists(x =>
            (x.PaymentStatus == PaymentStatus.RequiresAction
             || x.PaymentStatus == PaymentStatus.RequiresConfirmation
             || x.PaymentStatus == PaymentStatus.RequiresCapture)
            && (!option.HasValue || option.Value == x.PaymentOption));

        public bool HasRequiringPaymentMethod => Payments.Exists(x => x.PaymentStatus == PaymentStatus.RequiresPaymentMethod);

        public bool HasSucceededPayment => HasSucceededPaymentOption();

        public int PastSplitNumbers => Payments.Count(pm =>
            pm.PaymentStatus == PaymentStatus.Succeeded &&
            pm.PaymentOption == PaymentOptions.SplitPayments);

        public decimal? PendingSplitPaymentAmount => (SplitNumbers - PastSplitNumbers) * Payments.FirstOrDefault(pm =>
                pm.PaymentOption == PaymentOptions.SplitPayments && pm.PaymentStatus == PaymentStatus.Succeeded)?.TransferAmount;

        public bool HasPendingPayments => PendingSplitPaymentAmount != null &&
            PendingSplitPaymentAmount > 0;

        public bool HasSucceededPaymentOption(PaymentOptions? option = null) => Payments.Exists(x => x.PaymentStatus == PaymentStatus.Succeeded
                                                                                              && (!option.HasValue || option.Value == x.PaymentOption));

        public bool IsPaidAsSubscription => Payments.Exists(p =>
            p.PaymentOption == PaymentOptions.SplitPayments && p.PaymentStatus == PaymentStatus.Succeeded);

        public bool IsPaidAsEntireCourse => Payments.Exists(p =>
            p.PaymentOption == PaymentOptions.EntireCourse && p.PaymentStatus == PaymentStatus.Succeeded);

        public bool IsPaidAsSessionPackage => Payments.Exists(p =>
           p.PaymentOption == PaymentOptions.SessionsPackage && p.PaymentStatus == PaymentStatus.Succeeded);

        public bool IsPaidAsPerSession => Payments.Exists(p =>
           p.PaymentOption == PaymentOptions.PerSession && p.PaymentStatus == PaymentStatus.Succeeded);

        public PaymentOptions? RecentPaymentOption =>
            Payments?.OrderByDescending(p => p.DateTimeCharged)?.FirstOrDefault()?.PaymentOption;

        public string ActualPaymentStatus
        {
            get
            {
                var contrib = GetContribution();
                var contributionAndStandardAccountIdDic = GetStripeStandardAccounIdFromContribution(contrib).GetAwaiter().GetResult();
                FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
                var paymentOption = RecentPaymentOption;

                if (HasProcessingPayment)
                {
                    return PaymentStatus.Processing.GetName();
                }

                if ((RecentPaymentOption == PaymentOptions.Free || RecentPaymentOption == PaymentOptions.Trial) && HasActiveSubscription)
                {
                    return PaymentStatus.Succeeded.GetName();
                }

                if (ContributionType == nameof(ContributionCourse))
                {
                    if (HasUnconfirmedPayment)
                    {
                        return PaymentStatus.RequiresAction.GetName();
                    }

                    if (paymentOption == PaymentOptions.EntireCourse)
                    {
                        return Payments
                                   .Where(x => x.PaymentStatus != PaymentStatus.Canceled)
                                   .OrderByDescending(x => x.DateTimeCharged)
                                   .FirstOrDefault()?.PaymentStatus.GetName() ??
                               Constants.Contribution.Payment.Statuses.Unpurchased;
                    }

                    if (paymentOption == PaymentOptions.SplitPayments)
                    {
                        if (HasSucceededPayment)
                        {
                            return Payments.Exists(x => x.PaymentStatus == PaymentStatus.RequiresPaymentMethod)
                                ? Constants.Contribution.Payment.Statuses.ProceedSubscription
                                : PaymentStatus.Succeeded.GetName();
                        }
                    }
                }

                if (ContributionType == nameof(ContributionOneToOne))
                {
                    if (HasUnconfirmedPaymentOption(PaymentOptions.SessionsPackage))
                    {
                        return PaymentStatus.RequiresAction.GetName();
                    }

                    if (HasSucceededPaymentOption(PaymentOptions.SessionsPackage))
                    {
                        var contribution = GetContribution() as ContributionOneToOne;

                        var userPackages = contribution?.PackagePurchases.Where(p => p.UserId == ClientId);

                        if (userPackages != null && userPackages.Any(p => p.IsConfirmed && !p.IsCompleted))
                        {
                            return PaymentStatus.Succeeded.GetName();
                        }
                    }
                    if (HasSucceededPaymentOption(PaymentOptions.PerSession))
                    {
                        var contribution = GetContribution() as ContributionOneToOne;

                        var userPackages = contribution?.AvailabilityTimes.Where(p => p.BookedTimes.Any(c => c.ParticipantId == ClientId && c.IsCompleted!= true));

                        if (userPackages != null && userPackages.Any())
                        {
                            return PaymentStatus.Succeeded.GetName();
                        }
                    }

                }

                if (ContributionType == nameof(ContributionMembership) || ContributionType == nameof(ContributionCommunity))
                {
                    if (HasUnconfirmedPaymentOption(PaymentOptions.MonthlyMembership))
                    {
                        return PaymentStatus.RequiresAction.GetName();
                    }

                    if (HasActiveSubscription)
                    {
                        return PaymentStatus.Succeeded.GetName();
                    }
                }

                 return Constants.Contribution.Payment.Statuses.Unpurchased;
            }
        }

        //TODO: cache here
        public void FetchActualPaymentStatuses(Dictionary<string, string> contributionAndStandardAccountIdDic)
        {
            if (Payments.Any())
            {
                string standrdAccountId = string.Empty;
                var tasks = new List<Task>(Payments.Count);
                foreach (var payment in Payments.Where(p => p.PaymentStatus != PaymentStatus.Succeeded && p.PaymentStatus != PaymentStatus.Canceled))
                {
                    var task = Task.Run(async () =>
                    {
                        if (payment.IsTrial)
                        {
                            if (payment.PaymentStatus != PaymentStatus.Succeeded)//Temporary solution to reduce amount of requests to stripe
                            {
                                var invoice = await _memoryCache.GetOrCreateAsync("invoice_" + payment.InvoiceId, async entry =>
                                {
                                    entry.SetSlidingExpiration(TimeSpan.FromDays(2));
                                    return await GetInvoice(payment.InvoiceId, contributionAndStandardAccountIdDic.TryGetValue(ContributionId, out standrdAccountId) ? standrdAccountId : string.Empty);
                                });
                                payment.PaymentStatus =
                                    invoice?.Status.ToPaymentStatusEnum() ?? PaymentStatus.Processing;

                                if (payment.PaymentStatus == PaymentStatus.Paid)
                                {
                                    payment.PaymentStatus = PaymentStatus.Succeeded;
                                }
                            }
                        }
                        else
                        {
                            if (payment.PaymentStatus != PaymentStatus.Succeeded)//Temporary solution to reduce amount of requests to stripe
                            {
                                var paymentIntent = await _memoryCache.GetOrCreateAsync("payment_intent_" + payment.TransactionId, async entry =>
                                {
                                    entry.SetSlidingExpiration(TimeSpan.FromDays(2));
                                    return await GetPaymentIntent(payment.TransactionId, contributionAndStandardAccountIdDic.TryGetValue(ContributionId, out standrdAccountId) ? standrdAccountId : string.Empty);
                                });

                                payment.PaymentStatus =
                                    paymentIntent?.Status.ToPaymentStatusEnum() ?? PaymentStatus.Processing;
                            }
                        }
                    });

                    tasks.Add(task);
                }

                if(SubscriptionId == "-2")
				{
                    Subscription = new Subscription()
                    {
                        Id = "-2",
                        Status = Status.Active.ToString().ToLower()
                    };

                }
                else if (!string.IsNullOrEmpty(SubscriptionId))
                {
                    var getSubscriptionTask = Task.Run(async () =>
                    {
                        var subscription = await _memoryCache.GetOrCreateAsync("subscription_" + SubscriptionId, async entry =>
                        {
                            entry.SetSlidingExpiration(TimeSpan.FromDays(2));
                            return await GetSubscription(SubscriptionId, contributionAndStandardAccountIdDic.TryGetValue(ContributionId, out standrdAccountId) ? standrdAccountId : string.Empty);
                        });
                        Subscription = subscription;
                    });

                    tasks.Add(getSubscriptionTask);
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
        }
        private RequestOptions GetStandardAccountRequestOption(string standardAccountId)
        {
            if (string.IsNullOrEmpty(standardAccountId))
            {
                return null;
            }
            return new RequestOptions { StripeAccount = standardAccountId };
        }
        public async Task<Dictionary<string, string>> GetStripeStandardAccounIdFromContribution(ContributionBase contribution)
        {
            var contributionStandardAccountDic = new Dictionary<string, string>();
            string standardAccountId = string.Empty;
            if (contribution.PaymentType == PaymentTypes.Advance)
            {
                var user = await _unitOfWork.GetRepositoryAsync<Entity.Entities.User>().GetOne(m => m.Id == contribution.UserId);
                standardAccountId = user?.StripeStandardAccountId;
                contributionStandardAccountDic.Add(contribution.Id, standardAccountId);
            }
            return contributionStandardAccountDic;
        }
        public async Task<Cohere.Entity.Entities.User> GetClient()
        {
            var clientId = ClientId.ToLower().Contains("delete") ? ClientId.Split("-")?[1] : ClientId;
            return await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.User>()
                .GetOne(u => u.Id == clientId);
        }

        public ContributionBase GetContribution()
        {
            return _contributionRootService.GetOne(ContributionId)
                .GetAwaiter().GetResult();
        }

        private async Task<Subscription> GetSubscription(string subscriptionId, string standardAccountId)
        {
            if (subscriptionId is null)
            {
                return null;
            }

            try
            {
                return await _subscriptionService.GetAsync(subscriptionId, new SubscriptionGetOptions()
                {
                    Expand = new List<string>()
                {
                    "schedule",
                },
                },
                GetStandardAccountRequestOption(standardAccountId));
            }
            
            catch (StripeException)
            {
                return null;
            }
        }

        private async Task<Invoice> GetInvoice(string invoiceId, string standardAccountId)
        {
            if (invoiceId is null)
            {
                return null;
            }

            try
            {
                return await _invoiceService.GetAsync(invoiceId, null, GetStandardAccountRequestOption(standardAccountId));
            }
            catch (StripeException)
            {
                return null;
            }
        }

        private async Task<PaymentIntent> GetPaymentIntent(string transactionId, string standardAccountId)
        {
            if (transactionId == null)
            {
                return null;
            }

            try
            {
                return await _paymentIntentService.GetAsync(transactionId, null, GetStandardAccountRequestOption(standardAccountId));
            }
            catch (StripeException)
            {
                return null;
            }
        }
    }
}
