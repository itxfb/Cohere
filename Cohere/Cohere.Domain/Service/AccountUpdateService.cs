using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.AdminViewModels;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.Repository.Abstractions.Generic;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Stripe;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using entities = Cohere.Entity.Entities;

namespace Cohere.Domain.Service
{
    public class AccountUpdateService : IAccountUpdateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStripeService _stripeService;
        private readonly StripeAccountService _stripeAccountService;
        private readonly IServiceAsync<CountryViewModel, entities.Country> _countryService;
        private readonly IRepositoryAsync<entities.Account> _accountRepo;
        private readonly IRepositoryAsync<entities.User> _userRepo;
        private readonly ICommonService _commonService;
        private readonly INotificationService _notificationService;
        private readonly IActiveCampaignService _activeCampaignService;

        public AccountUpdateService
            (
            IUnitOfWork unitOfWork,
            IStripeService stripeService,
            StripeAccountService stripeAccountService,
            IServiceAsync<CountryViewModel, entities.Country> countryService,
            ICommonService commonService,
            INotificationService notificationService,
            IActiveCampaignService activeCampaignService
            )
        {
            _unitOfWork = unitOfWork;
            _stripeService = stripeService;
            _stripeAccountService = stripeAccountService;
            _countryService = countryService;
            _accountRepo = unitOfWork.GetGenericRepositoryAsync<entities.Account>();
            _userRepo = unitOfWork.GetGenericRepositoryAsync<entities.User>();
            _commonService = commonService;
            _notificationService = notificationService;
            _activeCampaignService = activeCampaignService;
        }
        public async Task<OperationResult> ChangeAgreementTypeAndAgreeToStripeAgreement(string email, string newCountry)
        {
            //Getting user account from email
            var requesterAccount = await _accountRepo.GetOne(c => c.Email.ToLower() == email.ToLower());
            if (requesterAccount == null) return OperationResult.Failure("Invalid Email Address");



            //Getting user Info from AccountId
            var requesterUser = await _userRepo.GetOne(c => c.AccountId == requesterAccount.Id);
            if (requesterUser == null) return OperationResult.Failure("Unable to find user");


            if (!string.IsNullOrEmpty(requesterUser.OldConnectedStripeAccountId))
            {
                throw new Exception("User already migrated!!");
            }

            var countryToSet = _countryService.Get(c => c.Name.ToLower() == newCountry.ToLower()).Result.FirstOrDefault();

            if (countryToSet != null)
            {
                requesterUser.CountryId = countryToSet.Id;
            }

            //Getting User Country of residence Info
            var country = await _countryService.GetOne(requesterUser.CountryId);

            //Creating new stripe Account with ServiceAgreementType --> 'full'
            var newCustomAccountResult = await _stripeAccountService.CreateCustomConnectAccountAsync(requesterAccount.Email, country.Alpha2Code, true, requesterUser);

            if (newCustomAccountResult.Failed) return OperationResult.Failure("Some error occured while creating new account");

            //Updating Stripe Accounts
            requesterUser.OldConnectedStripeAccountId = requesterUser.ConnectedStripeAccountId;
            requesterUser.ConnectedStripeAccountId = newCustomAccountResult.Payload;

            //Adding in beta user falgs
            requesterUser.IsBetaUser = true;
            requesterUser.ServiceAgreementType = "full";
            requesterUser.TransfersEnabled = false;
            requesterUser.IsSocialSecurityCheckPassed = false;

            //Updating
            await _userRepo.Update(requesterUser.Id, requesterUser);

            //TosAcceptance for the user.
            return await _stripeService.AgreeToStripeAgreement(requesterUser.ConnectedStripeAccountId, GetLocalIPAddress());
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public async Task<OperationResult> LinkStripePlanWithCohere(List<LinkingStripePurchasesViewModel> viewModel)
        {
            List<StripePlanPurchasesViewModel> ListofEmails_obj = new List<StripePlanPurchasesViewModel>();

            foreach (var paymentIntent_model in viewModel)
            {
                try
                {
                    //PaymentOption is not Valid
                    if (!Enum.TryParse<PaidTierOptionPeriods>(paymentIntent_model.PaymentOption, out var paymentOption))
                    {
                        ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, "Invalid PaymentOption, should only be Monthly, Annualy, EverySixMonth"));
                        continue;
                    }

                    //Getting user account from email
                    var requesterAccount = await _accountRepo.GetOne(c => c.Email.ToLower() == paymentIntent_model.Email.ToLower());
                    if (requesterAccount == null)
                    {
                        ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, "The given Email is not registered at Cohere."));
                        continue;
                    }

                    //Getting user Info from AccountId
                    var requesterUser = await _userRepo.GetOne(c => c.AccountId == requesterAccount.Id);
                    if (requesterUser == null)
                    {
                        ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, "Unable to find user at Cohere."));
                        continue;
                    }

                    var purchase = _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                        .GetOne(pt => pt.ClientId == requesterUser.Id && pt.SubscriptionId == paymentIntent_model.SubscriptionId)
                        .GetAwaiter().GetResult();
                    //plan already purchased
                    if (purchase != null)
                    {
                        ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, "The plan has been purchased already by the customer."));
                        continue;
                    }

                    var payment = new PaidTierPurchasePayment
                    {
                        TransactionId = paymentIntent_model.TransactionId,
                        PaymentOption = paymentOption,
                        DateTimeCharged = DateTime.Now,
                        PaymentStatus = PaymentStatus.Paid,
                        PeriodEnds = paymentIntent_model.PeriodEnds,
                        PurchaseAmount = paymentIntent_model.PurchaseAmount,
                        GrossPurchaseAmount = paymentIntent_model.GrossPurchaseAmount,
                        TransferAmount = paymentIntent_model.TransferAmount,
                    };

                    purchase ??= new PaidTierPurchase
                    {
                        ClientId = requesterUser.Id,
                        SubscriptionId = paymentIntent_model.SubscriptionId,
                        IsFirstPaymentHandled = true,
                        Payments = new List<PaidTierPurchasePayment>()
                    };
                    purchase.Payments.Add(payment);

                    await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>().Insert(purchase);

                    ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, "Success"));

                    //to send a notification to the admins
                    PaidTierOption currentPaidtierPlan = await NotifyAdminsForNewSignupOfPaidTierAccounts(paymentIntent_model, requesterUser, purchase);

                    //for active campaign purposes
                    SendActiveCampaignEventsToNewSignup(paymentIntent_model, requesterUser, currentPaidtierPlan);
                }
                catch (Exception e)
                {
                    ListofEmails_obj.Add(GetResponseObject(paymentIntent_model.Email, e.Message));
                }
            }

            return OperationResult.Success(null, ListofEmails_obj);
        }

        private void SendActiveCampaignEventsToNewSignup(LinkingStripePurchasesViewModel paymentIntent_model, User requesterUser, PaidTierOption currentPaidtierPlan)
        {
            var activeCampaignDeal = new ActiveCampaignDeal()
            {
                Value = paymentIntent_model.GrossPurchaseAmount.ToString()

            };
            var paidTierOption = _unitOfWork.GetRepositoryAsync<PaidTierOption>()
            .GetOne(p => p.Id == currentPaidtierPlan.Id).GetAwaiter().GetResult();

            var paidTierPeriod = default(PaidTierOptionPeriods);

            string paidTearOption = _activeCampaignService.PaidTearOptionToActiveCampaignDealCustomFieldValue(paidTierOption, paidTierPeriod);
            ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
            {
                CohereAccountId = requesterUser.AccountId,
                PaidTier = paidTearOption,
                PaidTierCreditCardStatus = EnumHelper<CreditCardStatus>.GetDisplayValue(CreditCardStatus.Normal),
            };
            _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
        }

        private async Task<PaidTierOption> NotifyAdminsForNewSignupOfPaidTierAccounts(LinkingStripePurchasesViewModel paymentIntent_model, User requesterUser, PaidTierPurchase purchase)
        {
            var subscriptionResult = _commonService.GetProductPlanSubscriptionAsync(purchase.SubscriptionId).GetAwaiter().GetResult();
            var subscription = subscriptionResult.Payload;
            var currentPaidtierPlan = _commonService.GetPaidTierByPlanId(subscription.Plan.Id).GetAwaiter().GetResult();
            var billingFrequency = currentPaidtierPlan.PaidTierInfo.GetStatus(subscription.Plan.Id);
            var customerName = $"{requesterUser.FirstName} {requesterUser.LastName}";
            var nextRenewelDate = _commonService.GetNextRenewelDateOfPlan(billingFrequency, purchase.CreateTime);

            await _notificationService.SendNotificationForNewSignupOfPaidtierAccount(customerName, paymentIntent_model.Email, billingFrequency.ToString(),
                currentPaidtierPlan.DisplayName, purchase.CreateTime, nextRenewelDate);
            return currentPaidtierPlan;
        }

        private StripePlanPurchasesViewModel GetResponseObject(string email, string message)
        {
            return new StripePlanPurchasesViewModel
            {
                Email = email,
                Message = message
            };
        }
    }
}