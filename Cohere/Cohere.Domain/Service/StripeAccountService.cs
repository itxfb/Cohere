using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Domain.Utils.Validators;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Stripe;
using CohereAccount = Cohere.Entity.Entities.Account;
using StripeAccount = Stripe.Account;

namespace Cohere.Domain.Service
{
    public class StripeAccountService
    {
        public const string AccountLinkSuccessUrl = nameof(AccountLinkSuccessUrl);
        public const string AccountLinkFailureUrl = nameof(AccountLinkFailureUrl);
        private const string MainCountryCode = "US";
        private const int StripeFileSizeLimit512Kb = 1024 * 512;

        private readonly CustomerService _customerService;
        private readonly AccountService _accountService;
        private readonly PaymentMethodService _paymentMethodService;
        private readonly ExternalAccountService _externalAccountService;
        private readonly AccountLinkService _accountLinkService;
        private readonly TokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IContributionStatusService _contributionStatusService;
        private readonly ILogger<StripeAccountService> _logger;
        private readonly string _accountLinkSuccessUrl;
        private readonly string _accountLinkFailureUrl;

        public StripeAccountService(
            CustomerService customerService,
            AccountService accountService,
            PaymentMethodService paymentMethodService,
            ExternalAccountService externalAccountService,
            AccountLinkService accountLinkService,
            TokenService tokenService,
            IUnitOfWork unitOfWork,
            IContributionStatusService contributionStatusService,
            ILogger<StripeAccountService> logger,
            Func<string, string> urlsResolver)
        {
            _customerService = customerService;
            _accountService = accountService;
            _paymentMethodService = paymentMethodService;
            _externalAccountService = externalAccountService;
            _accountLinkService = accountLinkService;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _contributionStatusService = contributionStatusService;
            _logger = logger;
            _accountLinkSuccessUrl = urlsResolver.Invoke(AccountLinkSuccessUrl);
            _accountLinkFailureUrl = urlsResolver.Invoke(AccountLinkFailureUrl);
        }

        public async Task<OperationResult<string>> CreateCustomerAsync(string customerEmail, bool createNew = false, string standardAccountId = null, string countryAlpha2Code = null, string postalCode = null)
        {
            if (string.IsNullOrWhiteSpace(customerEmail) || !Email.IsValid(customerEmail))
            {
                return OperationResult<string>.Failure($"{nameof(customerEmail)} argument must be not empty or invalid.");
            }

            if (!createNew && GetStripeCustomerByEmail(customerEmail, standardAccountId))
            {
                return OperationResult<string>.Failure($"{nameof(customerEmail)} stripe customer email already exists.");
            }


            try
            {

                var customer = await _customerService.CreateAsync(
                    new CustomerCreateOptions
                    {
                        Email = customerEmail,
                        Address = new AddressOptions
                        {
                            Country = countryAlpha2Code,
                            PostalCode = postalCode
                        }
                    },
                    GetStandardAccountRequestOption(standardAccountId)
                    ) ;
                return OperationResult<string>.Success(customer.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creation customer stripe account");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> UpdateCustomerBillingInfoForTaxInInvoice(string stripeCustomerId, string standardAccountId, string countryAlpha2Code)
        {
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                return OperationResult.Failure("Customer Id can not be null or empty");
            }
            var options = new CustomerUpdateOptions
            {
                Address = new AddressOptions
                {
                    Country = countryAlpha2Code.ToUpper()
                },
                Expand = new List<string> { "tax" }
            };
            try
            {
                var customerUpdated = await _customerService.UpdateAsync(stripeCustomerId, options, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(customerUpdated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during updating the customer for tax in invoices - {ex.Message}");
                return OperationResult.Failure("Error during updating the customer for tax in invoices");
            }
        }

        public async Task<OperationResult> GetCustomerAsync(string customerId, string standardAccountId = null)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return OperationResult.Failure($"{nameof(customerId)} argument must be not empty.");
            }

            try
            {
                var customer = await _customerService.GetAsync(customerId, options: null, GetStandardAccountRequestOption(standardAccountId));
                return OperationResult.Success(null, customer);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during getting customer stripe account");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<string>> CreateCustomConnectAccountAsync(string accountEmail, string accountCountryCode, bool IsBetaUser = false, User coach = null,
            Stream fileStream = null, bool createFromDashboard = false)
        {
            if (string.IsNullOrWhiteSpace(accountEmail))
            {
                return OperationResult<string>.Failure($"{nameof(accountEmail)} argument must be not empty.");
            }

            if (coach != null && !string.IsNullOrEmpty(coach.ConnectedStripeAccountId))
            {
                return OperationResult<string>.Failure("User already has a stripe custom account");
            }

            if (string.IsNullOrWhiteSpace(accountCountryCode))
            {
                return OperationResult<string>.Failure($"{nameof(accountCountryCode)} argument must be not empty.");
            }

            try
            {
                AccountCreateOptions model=null;
               
                string userName = coach.FirstName + " " + coach.LastName;
                model = new AccountCreateOptions
                {
                    Type = "custom",
                    Country = accountCountryCode?.ToUpper(),
                    RequestedCapabilities = new List<string>
                {
                    "transfers",
                    "card_payments"
                },
                    BusinessProfile = new AccountBusinessProfileOptions()
                    {
                        Url = "https://www.cohere.live/",
                    },
                    Email = accountEmail,
                    Settings = new AccountSettingsOptions()
                    {
                        Payouts = new AccountSettingsPayoutsOptions()
                        {
                            Schedule = new AccountSettingsPayoutsScheduleOptions()
                            {
                                Interval = "manual"
                            },
                        },
                        Payments = new AccountSettingsPaymentsOptions
                        {
                            StatementDescriptor = userName.Length>=22? userName.Substring(0,20): userName,
                        },
                        Branding = new AccountSettingsBrandingOptions()
                        {

                        },
                    },

                };

                if (coach.BrandingColors?.ContainsKey("PrimaryColorCode") == true)
                {
                    model.Settings.Branding.PrimaryColor = coach.BrandingColors["PrimaryColorCode"];
                }
                if (coach.BrandingColors?.ContainsKey("AccentColorCode") == true)
                {
                    model.Settings.Branding.SecondaryColor = coach.BrandingColors["AccentColorCode"];
                }
                if (fileStream is not null)
                {
                    var stripeFileId = UploadFileOnStripe(fileStream);
                    model.Settings.Branding.Logo = stripeFileId;
                }

                if (IsCrossborderClient(accountCountryCode))
                {
                    model.AddExtraParam("tos_acceptance[service_agreement]", "full");
                    model.AddExtraParam("tos_acceptance[ip]", GetLocalIPAddress());
                    model.AddExtraParam("tos_acceptance[date]", DateTimeOffset.FromUnixTimeSeconds(1609798905).UtcDateTime);
                }
               
                var account = await _accountService.CreateAsync(model);

                if (!createFromDashboard)
                {
                    return OperationResult<string>.Success(null, account.Id);
                }
                //creating the account using dashboard so uopdate user info and return the onboarding or verifcaiton link to redirect
                coach.IsBetaUser = true;
                coach.ServiceAgreementType = "full";
                coach.ConnectedStripeAccountId = account.Id;
                await _unitOfWork.GetRepositoryAsync<User>().Update(coach.Id, coach);
                var accountVerificationResult = await GenerateAccountVerificationLinkAsync(coach.ConnectedStripeAccountId, standardStripeAccountId: null, forStandardAccount: false);
                if (accountVerificationResult.Succeeded)
                {
                    return OperationResult<string>.Success(null, accountVerificationResult.Payload.ToString());
                }
                return OperationResult<string>.Failure(accountVerificationResult.Message);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creation stripe custom account");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        public async Task<OperationResult<string>> CreateStandardConnectAccountAsync(string accountEmail, string accountCountryCode, User coach, Stream fileStream = null, bool createFromDashboard = false)
        {
            if (string.IsNullOrWhiteSpace(accountEmail))
            {
                return OperationResult<string>.Failure($"{nameof(accountEmail)} argument must be not empty.");
            }

            if (coach is null)
            {
                return OperationResult<string>.Failure($"{nameof(coach)} argument must be not empty.");
            }

            if (string.IsNullOrWhiteSpace(accountCountryCode))
            {
                return OperationResult<string>.Failure($"{nameof(accountCountryCode)} argument must be not empty.");
            }

            if (!string.IsNullOrEmpty(coach.StripeStandardAccountId))
            {
                return OperationResult<string>.Failure("User already has a stripe standard account");
            }

            try
            {
                AccountCreateOptions model = null;
                string userName = coach.FirstName + " " + coach.LastName;
                model = new AccountCreateOptions
                {
                    Type = "standard",
                    Country = accountCountryCode?.ToUpper(),
                    BusinessProfile = new AccountBusinessProfileOptions()
                    {
                        Url = "https://www.cohere.live/",
                    },
                    Email = accountEmail,
                    Settings = new AccountSettingsOptions()
                    {
                        Payouts = new AccountSettingsPayoutsOptions()
                        {
                            Schedule = new AccountSettingsPayoutsScheduleOptions()
                            {
                                Interval = "manual",
                            },
                        },
                        Payments = new AccountSettingsPaymentsOptions
                        {
                            StatementDescriptor = userName.Length >= 22 ? userName.Substring(0, 20) : userName,
                        },
                        Branding = new AccountSettingsBrandingOptions()
                        {
                           
                        },
                    },
                };
                if (coach.BrandingColors?.ContainsKey("PrimaryColorCode") == true)
                {
                    model.Settings.Branding.PrimaryColor = coach.BrandingColors["PrimaryColorCode"];
                }
                if (coach.BrandingColors?.ContainsKey("AccentColorCode") == true)
                {
                    model.Settings.Branding.SecondaryColor = coach.BrandingColors["AccentColorCode"];
                }
                if (fileStream is not null)
                {
                    var stripeFileId = UploadFileOnStripe(fileStream);
                    model.Settings.Branding.Logo = stripeFileId;
                }
                var account = await _accountService.CreateAsync(model);

                if (!createFromDashboard)
                {
                    return OperationResult<string>.Success(null, account.Id);
                }

                //creating the account using dashboard so uopdate user info and return the onboarding or verifcaiton link to redirect
                coach.StripeStandardAccountId = account.Id;
                coach.IsStandardAccount = true;
                await _unitOfWork.GetRepositoryAsync<User>().Update(coach.Id, coach);
                //generate account Onboarding link from stripe
                var accountOnboardingResult = await GenerateAccountOnboardingLinkAsync(coach.StripeStandardAccountId, forStandardAccount: true);
                if (accountOnboardingResult.Succeeded)
                {
                    return OperationResult<string>.Success(null, accountOnboardingResult.Payload.ToString());
                }
                return OperationResult<string>.Failure(accountOnboardingResult.Message);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during creation stripe standard account");
                return OperationResult<string>.Failure(ex.Message);
            }
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
        public async Task<OperationResult> AddExternalAccountAsync(string connectedStripeAccountId, string source)
        {
            if (string.IsNullOrWhiteSpace(connectedStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(connectedStripeAccountId)} argument must be not empty.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return OperationResult.Failure($"{nameof(source)} argument must be not empty.");
            }

            try
            {
                var result = await _externalAccountService.CreateAsync(connectedStripeAccountId, new ExternalAccountCreateOptions
                {
                    ExternalAccount = source
                });

                return OperationResult.Success(null, result.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during adding external account");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> AttachCustomerPaymentMethodAsync(string customerStripeAccountId, string paymentMethodId, string standardAccountId)
        {
            if (string.IsNullOrWhiteSpace(customerStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(customerStripeAccountId)} argument must be not empty.");
            }

            if (string.IsNullOrWhiteSpace(paymentMethodId))
            {
                return OperationResult.Failure($"{nameof(paymentMethodId)} argument must be not empty.");
            }

            try
            {
                await _paymentMethodService.AttachAsync(paymentMethodId, new PaymentMethodAttachOptions
                {
                    Customer = customerStripeAccountId
                },GetStandardAccountRequestOption(standardAccountId));

                return OperationResult.Success(null);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during attaching customer payment method");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> AttachCustomerPaymentMethodByCardTokenAsync(string customerStripeAccountId, string cardToken, string standardAccountId)
        {
            if (string.IsNullOrWhiteSpace(customerStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(customerStripeAccountId)} argument must be not empty.");
            }

            if (string.IsNullOrWhiteSpace(cardToken))
            {
                return OperationResult.Failure($"{nameof(cardToken)} argument must be not empty.");
            }

            try
            {
                var RequestOptionForStandardAccount = GetStandardAccountRequestOption(standardAccountId);
                var token = await _tokenService.GetAsync(cardToken, options: null, RequestOptionForStandardAccount);

                var customerPaymentMethods = await _paymentMethodService.ListAsync(new PaymentMethodListOptions
                {
                    Customer = customerStripeAccountId,
                    Type = "card"
                }, RequestOptionForStandardAccount);

                var paymentMethod =
                    customerPaymentMethods.Data.Find(x => x.Card.Fingerprint == token.Card.Fingerprint);

                if (paymentMethod == null)
                {
                    paymentMethod = await _paymentMethodService.CreateAsync(new PaymentMethodCreateOptions
                    {
                        Card = new PaymentMethodCardCreateOptions { Token = cardToken },
                        Type = "card"
                    }, RequestOptionForStandardAccount);

                    await _paymentMethodService.AttachAsync(paymentMethod.Id, new PaymentMethodAttachOptions
                    {
                        Customer = customerStripeAccountId
                    }, RequestOptionForStandardAccount);
                }

                return OperationResult.Success(null, paymentMethod.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during attaching customer payment method for customer stripe account {customerStripeAccountId}");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> GenerateAccountVerificationLinkAsync(string connectedStripeAccountId, string standardStripeAccountId, bool forStandardAccount)
        {
            if (!forStandardAccount && string.IsNullOrWhiteSpace(connectedStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(connectedStripeAccountId)} argument must be not empty.");
            }

            if (forStandardAccount && string.IsNullOrEmpty(standardStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(standardStripeAccountId)} argument must be not empty for standard account.");
            }

            try
            {
                var result = await _accountLinkService.CreateAsync(new AccountLinkCreateOptions
                {
                    Account = forStandardAccount ? standardStripeAccountId : connectedStripeAccountId,
                    RefreshUrl = _accountLinkFailureUrl,
                    ReturnUrl = _accountLinkSuccessUrl,
                    Type = forStandardAccount ? "account_onboarding" : "custom_account_verification"
                });

                return OperationResult.Success(null, result.Url);
            }
            catch (StripeException ex)
            {
                if (forStandardAccount)
                {
                    _logger.LogError(ex, $"error generate stripe verification link for standard account {standardStripeAccountId}");
                }
                else
                {
                    _logger.LogError(ex, $"error generate stripe verification link for connected account {connectedStripeAccountId}");
                }
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> GenerateAccountOnboardingLinkAsync(string connectedStripeAccountId, bool forStandardAccount = false)
        {
            if (string.IsNullOrWhiteSpace(connectedStripeAccountId))
            {
                return OperationResult.Failure($"{nameof(connectedStripeAccountId)} argument must be not empty.");
            }

            try
            {
                var result = await _accountLinkService.CreateAsync(new AccountLinkCreateOptions
                {
                    Account = connectedStripeAccountId,
                    RefreshUrl = _accountLinkFailureUrl,
                    ReturnUrl = _accountLinkSuccessUrl,
                    Type = "account_onboarding",
                    Collect = forStandardAccount ? null : "eventually_due"  //collect field only in case of custom account not for standard
                });

                return OperationResult.Success(null, result.Url);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error generate stripe onboarding link for connected account {connectedStripeAccountId}");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> GetConnectedAccountAsync(string connectedAccountId)
        {
            if (string.IsNullOrWhiteSpace(connectedAccountId))
            {
                return OperationResult.Failure($"{nameof(connectedAccountId)} argument must be not empty.");
            }

            try
            {
                var account = await _accountService.GetAsync(connectedAccountId);
                return OperationResult.Success(null, account);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"error during getting connected account {connectedAccountId}");
                return OperationResult.Failure(ex.Message);
            }
        }

        public async Task<OperationResult> RemoveBankAccount(string coachAccountId, string bankAccountId)
        {
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == coachAccountId);
            if (requestorUser == null)
            {
                return OperationResult.Failure($"Unable to find user with AccountId: {coachAccountId}");
            }

            try
            {
                var connectedStripeAccountId = requestorUser.ConnectedStripeAccountId;
                var externalAccount = await _externalAccountService.GetAsync(connectedStripeAccountId, bankAccountId);

                if (externalAccount is BankAccount bankAccount)
                {
                    await _externalAccountService.DeleteAsync(connectedStripeAccountId, bankAccount.Id);
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during removing bank account");
                return OperationResult.Failure($"Vendor's exceptions occured: {ex.Message}");
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> SetBankAccountAsDefault(string coachAccountId, string bankAccountId)
        {
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == coachAccountId);

            if (requestorUser == null)
            {
                return OperationResult.Failure($"Unable to find user with AccountId: {coachAccountId}");
            }

            try
            {
                var connectedStripeAccountId = requestorUser.ConnectedStripeAccountId;
                var externalAccount = await _externalAccountService.GetAsync(connectedStripeAccountId, bankAccountId);

                if (externalAccount is BankAccount)
                {
                    var options = new ExternalAccountUpdateOptions()
                    {
                        DefaultForCurrency = true
                    };
                    await _externalAccountService.UpdateAsync(connectedStripeAccountId, bankAccountId, options);
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during setting bank account");
                return OperationResult.Failure($"Vendor's exceptions occured: {ex.Message}");
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> ListBankAccounts(string coachAccountId)
        {
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == coachAccountId);

            if (coachUser == null)
            {
                return OperationResult.Failure($"Unable to find user with AccountId: {coachAccountId}");
            }
            var account = await _unitOfWork.GetRepositoryAsync<Entity.Entities.Account>().GetOne(u => u.Id == coachAccountId);
            string standardConnectedStripeAccountId = string.Empty;
            var bankAccountsVms = new List<BankAccountAttachedViewModel>();
            var standardBankAccountsVms = new List<BankAccountAttachedViewModel>();
            try
            {
                var connectedStripeAccountId = coachUser.ConnectedStripeAccountId;
                var externalAccounts = await _externalAccountService.ListAsync(connectedStripeAccountId);
                foreach (var externalAccount in externalAccounts)
                {
                    if (!(externalAccount is BankAccount bankAccount))
                    {
                        continue;
                    }

                    bankAccountsVms.Add(new BankAccountAttachedViewModel
                    {
                        Id = bankAccount.Id,
                        BankName = bankAccount.BankName,
                        Last4 = bankAccount.Last4,
                        IsDefaultForCurrency = bankAccount.DefaultForCurrency.GetValueOrDefault()
                    });
                }
                if (coachUser.IsStandardAccount && !string.IsNullOrWhiteSpace(coachUser.StripeStandardAccountId))
                {
                    standardConnectedStripeAccountId = coachUser.StripeStandardAccountId;
                    var standardExternalAccounts = await _externalAccountService.ListAsync(standardConnectedStripeAccountId);
                    foreach (var externalAccount in standardExternalAccounts)
                    {
                        if (!(externalAccount is BankAccount bankAccount))
                        {
                            continue;
                        }

                        bankAccountsVms.Add(new BankAccountAttachedViewModel
                        {
                            Id = bankAccount.Id,
                            BankName = bankAccount.BankName,
                            Last4 = bankAccount.Last4,
                            IsDefaultForCurrency = bankAccount.DefaultForCurrency.GetValueOrDefault(),
                            IsStandard = true
                        });
                    }
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "error during list bank account");
                return OperationResult.Failure($"Vendor's exceptions occured: {ex.Message}");
            }
            account.IsStandardBankAccountConnected = (bankAccountsVms.Count(a => a.IsStandard) > 0 && !string.IsNullOrWhiteSpace(standardConnectedStripeAccountId));
            
            account.IsBankAccountConnected = (bankAccountsVms.Count(a => !a.IsStandard) > 0 && !string.IsNullOrEmpty(coachAccountId) && account != null);

            await _unitOfWork.GetRepositoryAsync<Entity.Entities.Account>().Update(account.Id, account);
            return OperationResult.Success(string.Empty, bankAccountsVms);
        }

        public OperationResult HandleAccountUpdatedStripeEvent(Event @event)
        {
            if (@event.Data.Object is StripeAccount connectedAccount)
            {
                User user = null;
                if (connectedAccount.Type == Constants.Stripe.AccountType.Standard)
                {
                    user = _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.StripeStandardAccountId == connectedAccount.Id).GetAwaiter().GetResult();
                }
                else if (connectedAccount.Type == Constants.Stripe.AccountType.Custom)
                {
                    user = user = _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.ConnectedStripeAccountId == connectedAccount.Id).GetAwaiter().GetResult();
                }

                if (user == null)
                {
                    return OperationResult.Failure(Constants.Contribution.Payment.StripeWebhookErrors.UserNotFound);
                }

                var transfersEnabled = connectedAccount.Capabilities.Transfers ==
                                       Constants.Contribution.Payment.ConnectAccount.TransfersCapability.Active;

                var isSocialSecurityCheckPassed = // (connectedAccount.Individual?.SsnLast4Provided ?? false) && Removing this check as stripe says that SSnLast4Provided not requierd in case of individual accounts
                        (connectedAccount?.Individual?.Dob?.Day != null) &&
                        (connectedAccount?.Individual?.Dob?.Year != null) &&
                        (connectedAccount?.Individual?.Dob?.Month != null);

                if (transfersEnabled || connectedAccount.PayoutsEnabled || user.IsSocialSecurityCheckPassed != isSocialSecurityCheckPassed)
                {
                    if (connectedAccount.Type == Constants.Stripe.AccountType.Custom)
                    {
                        user.TransfersEnabled = transfersEnabled;
                        if (connectedAccount.BusinessType == "individual" && connectedAccount.Individual.Verification.Status == "verified")
                        {
                            user.TransfersNotLimited = true;
                        }
                    }
                    if (connectedAccount.Type == Constants.Stripe.AccountType.Standard)
                    {
                        user.StandardAccountTransfersEnabled = transfersEnabled;
                        user.PayoutsEnabled = true;
                        if (connectedAccount.BusinessType == "individual" && connectedAccount.Individual.Verification.Status == "verified")
                        {
                            user.StandardAccountTransfersNotLimited = true;
                        }
                    }
                    user.PayoutsEnabled = connectedAccount.PayoutsEnabled;
                    user.IsSocialSecurityCheckPassed = isSocialSecurityCheckPassed;
                    user = _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user).GetAwaiter().GetResult();
                    //var account = _unitOfWork.GetRepositoryAsync<CohereAccount>().GetOne(a => a.Id == user.AccountId).GetAwaiter().GetResult();
                    if (transfersEnabled) // && account.IsEmailConfirmed)
                    {
                        _contributionStatusService.ExposeContributionsToReviewAsync(user.Id).GetAwaiter().GetResult();
                    }
                }

                return OperationResult.Success(null);
            }

            return OperationResult.Failure($"The data of the event with ID: '{@event.Id}' is not compatible with '{typeof(StripeAccount).FullName}' type");
        }

        private bool IsCrossborderClient(string accountCountryCode) => accountCountryCode != MainCountryCode;

        public bool GetStripeCustomerByEmail(string email, string standardAccountId = null)
        {
            var customer = _customerService.List(new CustomerListOptions
            {
                Email = email
            },
            GetStandardAccountRequestOption(standardAccountId)
            );

            return customer.Any();
        }

        private RequestOptions GetStandardAccountRequestOption(string standardAccountId)
        {
            if (string.IsNullOrEmpty(standardAccountId))
            {
                return null;
            }
            return new RequestOptions { StripeAccount = standardAccountId };
        }

        public OperationResult GetCustomerAccountList(string email, string standardAccountId = null)
        {
            var customer = _customerService.List(new CustomerListOptions
            {
                Email = email
            },
            GetStandardAccountRequestOption(standardAccountId)
            );
            List<StripeCustomerAccount> customerAccounts = new List<StripeCustomerAccount>();
            customer.ToList().ForEach(ac => customerAccounts.Add(new StripeCustomerAccount { CustomerId = ac.Id, Currency = ac.Currency }));
            return OperationResult.Success(String.Empty, customerAccounts);
        }

        public OperationResult GetCustomerAccountListForInvoice(string email, string standardAccountId = null)
        {
            var customerAccountList = _customerService.List(new CustomerListOptions
            {
                Email = email
            },
            GetStandardAccountRequestOption(standardAccountId)
            );
            return OperationResult.Success(String.Empty, customerAccountList.ToList());
        }


        public void SetCustomColorForCheckout(string stripeAccountID, string primaryColor, string secondaryColor, string stripeStandardAccountId = null)
        {
            try
            {
                var option = new AccountUpdateOptions
                {
                    Settings = new AccountSettingsOptions
                    {
                        Branding = new AccountSettingsBrandingOptions
                        {
                            PrimaryColor = primaryColor,
                            SecondaryColor = secondaryColor
                        }
                    }
                };
                if (!string.IsNullOrEmpty(stripeStandardAccountId))
                {
                    _accountService.Update(stripeStandardAccountId, option);
                }
                if (!string.IsNullOrEmpty(stripeAccountID))
                {
                    _accountService.Update(stripeAccountID, option);
                }

            }
            catch(Exception ex)
            {
                _logger.LogError(ex,"Exception thrown while updating the strip account setting for branding colors.");
            }
        }



        public void SetCustomLogoForCheckout(string stripeAccountID, Stream fileStream, string stripeStandardAccountId = null)
        {
            if (!string.IsNullOrEmpty(stripeAccountID))
            {
                var stripeFileIdForCustomAccount = UploadFileOnStripe(fileStream);
                if (!string.IsNullOrEmpty(stripeFileIdForCustomAccount))
                {

                    var brandingoption = new AccountUpdateOptions
                    {
                        Settings = new AccountSettingsOptions
                        {
                            Branding = new AccountSettingsBrandingOptions
                            {
                                Logo = stripeFileIdForCustomAccount,
                            }
                        }
                    };
                    _accountService.Update(stripeAccountID, brandingoption);
                } 
            }
           
            if(stripeStandardAccountId != null)
            {
                fileStream.Position = 0; //rewind the file stream object
                var stripeFileIdForStandardAccount = UploadFileOnStripe(fileStream);
                if (!string.IsNullOrEmpty(stripeFileIdForStandardAccount))
                {

                    var brandingoption = new AccountUpdateOptions
                    {
                        Settings = new AccountSettingsOptions
                        {
                            Branding = new AccountSettingsBrandingOptions
                            {
                                Logo = stripeFileIdForStandardAccount,
                            }
                        }
                    };
                    _accountService.Update(stripeStandardAccountId, brandingoption);
                }
            }
        }

        public string UploadFileOnStripe(Stream fileStream)
        {
            if (fileStream.Length <= StripeFileSizeLimit512Kb)
            {
                try
                {
                    var services = new FileService();
                    var option = new FileCreateOptions
                    {
                        File = fileStream,
                        Purpose = FilePurpose.BusinessLogo,
                    };

                    Stripe.File upload = services.Create(option);
                    return upload.Id;
                }   
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while uploading file on stripe server storage.");
                    return null;
                }
            }
            else
            {
                _logger.LogInformation("Too larger file. The max size for stripe checkout logo is 512Kb ");
                return null;
            }
        }

        public async Task<OperationResult> CreateDefaultSripeAccountforUser(string accountEmail, string alpha2CountryCode, User coachUser, Stream fileStream = null)
        {
            if (Constants.DefaultStripeAccount == Constants.Stripe.AccountType.Standard)
            {
                var createStandardAccountResult = await CreateStandardConnectAccountAsync(accountEmail, alpha2CountryCode, coachUser, fileStream);
                if (createStandardAccountResult.Failed)
                {
                    return createStandardAccountResult;
                }

                coachUser.StripeStandardAccountId = createStandardAccountResult.Payload;
                coachUser.IsStandardAccount = true;
                coachUser.DefaultPaymentMethod = PaymentTypes.Advance;
                return OperationResult.Success();
            }
            else
            {
                var createCustomAccountResult = await CreateCustomConnectAccountAsync(accountEmail, alpha2CountryCode, coachUser.IsBetaUser, coachUser, fileStream);
                if (createCustomAccountResult.Failed)
                {
                    return createCustomAccountResult;
                }
                coachUser.ConnectedStripeAccountId = createCustomAccountResult.Payload;
                coachUser.IsBetaUser = true;
                coachUser.ServiceAgreementType = "full";
                coachUser.DefaultPaymentMethod = PaymentTypes.Simple;
                return OperationResult.Success();
            }
        }
    }
}
