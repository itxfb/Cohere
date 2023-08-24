using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using AutoMapper;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Notification;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Ical.Net.CalendarComponents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nancy.Json;
using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg.OpenPgp;
using RestSharp;
using ZoomNet;
using static Cohere.Domain.Utils.Constants;

namespace Cohere.Domain.Service
{
    public class NotificationService : INotificationService
    {
        private readonly ICommonService _commonService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;
        private readonly ICalendarSyncService _calendarSyncService;
        private readonly IJobScheduler _jobScheduler;
        private readonly IFCMService _fcmService;
        private readonly string _contributionLinkTemplate;
        private readonly string _loginLink;
        private readonly string _emailVerificationLink;
        private readonly string _passwordRestorationRedirectUrl;
        private readonly string _unsubscribeEmailLink;
        private readonly string _signUpUrl;
        private readonly string _sessionNotificationSourceAddress;
        private readonly string _affiliateLinkTemplate;
        private readonly IContributionRootService _contributionRootService;
        private readonly IServiceAsync<TimeZoneViewModel, Entity.Entities.TimeZone> _timeZoneService;
        private readonly ClientUrlsSettings _urlSettings;

        public const string ContributionLinkTemplate = "ContributionLinkTemplate";
        public const string LoginLink = "LoginLink";
        public const string SignUpPath = "SignUpPath";
        public const string EmailVerificationLink = "EmailVerificationLink";
        public const string PasswordRestorationRedirectUrl = "PasswordRestorationRedirectUrl";
        public const string UnsubscribeEmailsLink = "UnsubscribeEmailsLink";
        public const string SessionNotificationSourceAddress = "SessionNotificationSourceAddress";
        public const string AffiliateLinkTemplate = "AffiliateLinkTemplate";

        //TODO : Change resolvers to IOptions Probably
        public NotificationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEmailService emailService,
            ILogger<NotificationService> logger,
            ICalendarSyncService calendarSyncService,
            IJobScheduler jobScheduler,
            IFCMService fcmService,
            Func<string, string> contributionLinkTemplateResolver,
            Func<string, string> loginLinkResolver,
            Func<string, string> emailVerificationLinkResolver,
            Func<string, string> sessionNotificationSourceAddressResolver,
            Func<string, string> passwordRestorationRedirectUrlResolver,
            Func<string, string> unsubscribeEmailsLinkResolver,
            Func<string, string> signUpPathResolver,
            Func<string, string> affiliateLinkTemplateResolver, IContributionRootService contributionRootService,
            IOptions<ClientUrlsSettings> clientUrlsOptions,
            IServiceAsync<TimeZoneViewModel, Entity.Entities.TimeZone> timeZoneService, ICommonService commonService) //TODO: refactor this with IOptions Pattern
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _emailService = emailService;
            _logger = logger;
            _calendarSyncService = calendarSyncService;
            _jobScheduler = jobScheduler;
            _fcmService = fcmService;
            _contributionLinkTemplate = contributionLinkTemplateResolver.Invoke(ContributionLinkTemplate);
            _loginLink = loginLinkResolver.Invoke(LoginLink);
            _emailVerificationLink = emailVerificationLinkResolver.Invoke(EmailVerificationLink);
            _passwordRestorationRedirectUrl =
                passwordRestorationRedirectUrlResolver.Invoke(PasswordRestorationRedirectUrl);
            _unsubscribeEmailLink = unsubscribeEmailsLinkResolver.Invoke(UnsubscribeEmailsLink);
            _signUpUrl = signUpPathResolver.Invoke(SignUpPath);
            _affiliateLinkTemplate = affiliateLinkTemplateResolver(AffiliateLinkTemplate);
            _sessionNotificationSourceAddress =
                sessionNotificationSourceAddressResolver(SessionNotificationSourceAddress);
            _contributionRootService = contributionRootService;
            _urlSettings = clientUrlsOptions.Value;
            _timeZoneService = timeZoneService;
            _commonService = commonService;
        }

        public async Task SendPaymentSuccessNotification(Purchase purchase, ContributionBase contribution,
            SucceededPaymentEmailViewModel paymentInfo)
        {
            string currencySymbol = null;
            string currencyCode = null;
            if(!string.IsNullOrEmpty(contribution.DefaultSymbol)) currencySymbol = contribution.DefaultSymbol;
            if(!string.IsNullOrEmpty(contribution.DefaultCurrency)) currencyCode = contribution.DefaultCurrency;
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == purchase.ClientId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == client.AccountId);

            if (!clientAccount.IsEmailNotificationsEnabled)
            {
                return;
            }
            
            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.PaymentNotification);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{nameOfContribution}", contribution.Title)
                .Replace("{clientFirstName}", client.FirstName)
                .Replace("{orderNumber}", purchase.Id)
                .Replace("{perMonthSuffix}",
                    paymentInfo == null ? "0" : GetPerTotalAmountPeriodSuffix(paymentInfo.PaymentOption))
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink);

            if (contribution.PaymentInfo.CoachPaysStripeFee)
            {
                personalizedHtmlTemplate = personalizedHtmlTemplate
                    .Replace("{processingFees}", string.Empty)
                    .Replace("{orderTotal}",
                    paymentInfo == null ? "0" : $"Cost of the program: {currencySymbol ?? "$"}{paymentInfo.TotalAmount.ToString(CultureInfo.CurrentUICulture)} {currencyCode?.ToUpper() ?? "USD"}")
                    .Replace("{contributionWithPrice}", string.Empty);
            }
            else
            {
                decimal? totalClientFee = 0;

                decimal orderTotal = paymentInfo == null ? 0 : paymentInfo.TotalAmount;
                decimal processingFee = paymentInfo == null ? 0 : paymentInfo.ProcessingFee;
                decimal actualPurchasePrice = paymentInfo == null ? 0 : paymentInfo.PurchasePrice;

                if (paymentInfo != null && paymentInfo.PurchasePrice < contribution.PaymentInfo.Cost) 
                {
                    if (contribution.PaymentInfo.PackageSessionDiscountPercentage == null)
                        totalClientFee = paymentInfo.TotalAmount - contribution.PaymentInfo.Cost;
                    else
                        totalClientFee = paymentInfo.TotalAmount - (contribution.PaymentInfo.Cost - (contribution.PaymentInfo.Cost * contribution.PaymentInfo.PackageSessionDiscountPercentage / 100));
                    orderTotal = paymentInfo.TotalAmount;
                    processingFee = totalClientFee ?? 0;
                    actualPurchasePrice = orderTotal - processingFee;
                }

                personalizedHtmlTemplate = personalizedHtmlTemplate
                .Replace("{processingFees}",
                    paymentInfo == null ? "0" : $"Processing fees: {currencySymbol ?? "$"}{processingFee.ToString(CultureInfo.CurrentUICulture)} {currencyCode?.ToUpper() ?? "USD"}<br>")
                .Replace("{orderTotal}",
                    paymentInfo == null ? "0" : $"Order Total: {currencySymbol ?? "$"}{orderTotal.ToString(CultureInfo.CurrentUICulture)} {currencyCode?.ToUpper() ?? "USD"}")
                .Replace("{contributionWithPrice}",
                   $"{contribution.Title}: {currencySymbol ?? "$"}{actualPurchasePrice.ToString(CultureInfo.CurrentUICulture)} {currencyCode?.ToUpper() ?? "USD"}<br>");
            }

            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            if (purchaseVm.RecentPaymentOption == PaymentOptions.SplitPayments)
            {
                var splitPaymentsParagraph =
                    $"<br>{purchase.SplitNumbers.ToString()} split payments. Your current payment {currencySymbol ?? "$"}{paymentInfo?.CurrentAmount.ToString(CultureInfo.CurrentUICulture)} {currencyCode?.ToUpper() ?? "USD"}";

                personalizedHtmlTemplate = personalizedHtmlTemplate
                    .Replace("{splitPaymentsMessage}", splitPaymentsParagraph);
            }
            else
            {
                personalizedHtmlTemplate = personalizedHtmlTemplate
                    .Replace("{splitPaymentsMessage}", string.Empty);
            }

            await _emailService.SendAsync(clientAccount.Email, $"You are confirmed for {contribution.Title}",
                personalizedHtmlTemplate);
        }

        public async Task SendClientEnrolledNotification(ContributionBase contribution, string clientName, string clientEmail, string paidAmount)
        {

            var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            string currencySymbol = "$";
            string currencyCode = "USD";
            if (!string.IsNullOrEmpty(contribution.DefaultSymbol)) currencySymbol = contribution.DefaultSymbol;
            if (!string.IsNullOrEmpty(contribution.DefaultCurrency)) currencyCode = contribution.DefaultCurrency?.ToUpper();
            var authorAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == authorUser.AccountId);

            #region Push Notification
            try
            {
                string amount = currencySymbol + paidAmount + " " + currencyCode;
                var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email.ToLower() == clientName.ToLower());
                var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == clientAccount.Id);
                if (contribution is SessionBasedContribution sessionBasedContribution)
                {
                    await _fcmService.SendPaidGroupContributionJoinPushNotification(contribution.Id, amount, client.Id);
                }
                else
                {
                    await _fcmService.SendPaidOneToOneBookPushNotification(contribution.Id, amount, client.Id);

                }
            }
            catch
            {

            }
            #endregion

            if (!IsEmailNotificationEnabled(authorAccount))
            {
                return;
            }
            // todo: remove comment

            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.NewSale);
            var subject = "Congrats! Payment of {currencySymbol}{paidAmount} from {clientName}";

            (string template, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.NewSale), emailHtmlTemplate, contribution,authorUser);
            emailHtmlTemplate = template;
            subject = updatedSubject;

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{contributionName}", contribution.Title)
                .Replace("{cohealerFirstName}", authorUser.FirstName)
                .Replace("{clientEmail}", clientEmail)
                .Replace("{currencySymbol}", currencySymbol ?? "$")
                .Replace("{paidAmount}", paidAmount)
                .Replace("{currencyCode}", currencyCode ?? "USD")
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink)
                .Replace("{clientName}", clientName);

            subject = subject
               .Replace("{contributionName}", contribution.Title)
               .Replace("{cohealerFirstName}", authorUser.FirstName)
               .Replace("{clientEmail}", clientEmail)
               .Replace("{currencySymbol}", currencySymbol ?? "$")
               .Replace("{paidAmount}", paidAmount)
               .Replace("{currencyCode}", currencyCode ?? "USD")
               .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
               .Replace("{year}", DateTime.UtcNow.Year.ToString())
               .Replace("{loginLink}", _loginLink)
               .Replace("{clientName}", clientName);

            await _emailService.SendAsync(authorAccount.Email, subject, personalizedHtmlTemplate);
        }
       
        public async Task SendClientFreeEnrolledNotification(ContributionBase contribution, string clientEmail, string clientName)
        {
            var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            var authorAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == authorUser.AccountId);

            // todo: remove comment
            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.NewFreeSale);
            var subject = "Congrats!  {clientEmail} just enrolled";

            (string template, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.NewFreeSale), emailHtmlTemplate, contribution,authorUser);
            emailHtmlTemplate = template;
            subject = updatedSubject;

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{contributionName}", contribution.Title)
                .Replace("{cohealerFirstName}", authorUser.FirstName)
                .Replace("{clientName}", clientName)
                .Replace("{clientEmail}", clientEmail)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink);

                 subject = subject
                .Replace("{contributionName}", contribution.Title)
                .Replace("{cohealerFirstName}", authorUser.FirstName)
                .Replace("{clientName}", clientName)
                .Replace("{clientEmail}", clientEmail)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendAsync(authorAccount.Email, subject, personalizedHtmlTemplate);
        }
        public async Task SendContributionInvitationMessage(ContributionBase contributionToShare,
            IEnumerable<string> emailAddresses, string inviterAccountId)
        {
            var inviter = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == inviterAccountId);

            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.ShareContribution);

            var contributionLinkTemplate = _contributionLinkTemplate.Replace("{id}", contributionToShare.Id);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{initiatorFirstName}", inviter.FirstName)
                .Replace("{initiatorLastName}", inviter.LastName)
                .Replace("{contributionTitle}", contributionToShare.Title)
                .Replace("{contributionLink}", contributionLinkTemplate)
                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString());
            await _emailService.SendAsync(emailAddresses,
                $"{inviter.FirstName} warmly invites you to: {contributionToShare.Title}", personalizedHtmlTemplate);
        }

        public async Task SendReferralLinkEmailMessage(IEnumerable<string> emailAdresses, string affiliateAccountId,
            string inviteCode)
        {
            var inviter = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == affiliateAccountId);

            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Affiliate.ShareReferal);

            var affiliateLink = _affiliateLinkTemplate.Replace("{inviteCode}", inviteCode);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{initiatorFirstName}", inviter.FirstName)
                .Replace("{initiatorLastName}", inviter.LastName)
                .Replace("{affiliateLink}", affiliateLink)
                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(emailAdresses,
                $"{inviter.FirstName} {inviter.LastName} has invited you to join Cohere!", personalizedHtmlTemplate);
        }

        public async Task SendCustomEmailFromCohealer(string cohealerAccountId, string clientUserId,
            string customMessage)
        {
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == clientUserId);
            var clientAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == clientUser.AccountId);
            var cohealerUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.AccountId == cohealerAccountId);

            if (!IsEmailNotificationEnabled(clientAccount))
            {
                return;
            }

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Communication.CustomCohealerMessage);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{cohealerFirstName}", cohealerUser.FirstName)
                .Replace("{cohealerFullName}", $"{cohealerUser.FirstName} {cohealerUser.LastName}")
                .Replace("{customMessage}", customMessage)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendAsync(clientAccount.Email, $"New Message From {cohealerUser.FirstName}",
                personalizedHtmlTemplate);
        }

        public async Task SendCoachJoinedNotificationToAllAdmins(string cohealerEmail, string invitedBy,
            User cohealerUser)
        {
            var allAdminsEmail = await GetAllAdminsEmail();

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Account.NewCohealerAdminNotification);

            var cohealerName = $"{cohealerUser.FirstName} {cohealerUser.LastName}";

            var invitedByPlaceholder = string.IsNullOrEmpty(invitedBy)
                ? string.Empty
                : await GetInvitedByPlaceholderValue(invitedBy);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{cohealerName}", cohealerName)
                .Replace("{cohealerCreationTime}", cohealerUser.CreateTime.ToString())
                .Replace("{cohealerEmail}", cohealerEmail)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{invitedBy}", invitedByPlaceholder)
                .Replace("{year}", DateTime.UtcNow.Year.ToString())
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendAsync(allAdminsEmail, $"{cohealerName} created an account",
                personalizedHtmlTemplate);
        }

        public async Task SendPurchaseFailNotifcationToCoach(string coachEmail, string clientFirstName, string clientLastName, string clientEmail, string errorMessage, string contributionTitle)
        {
            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientPurchaseError);

            var clientName = $"{clientFirstName} {clientLastName}";

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{ClientName}", clientName)
                .Replace("{ClientEmail}", clientEmail)
                .Replace("{ContributionName}", contributionTitle)
                .Replace("{ErrorMessage}", errorMessage)
                .Replace("{loginLink}", _loginLink)
                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);

            await _emailService.SendAsync(coachEmail, $"{clientName} failed to purchase {contributionTitle}",
                personalizedHtmlTemplate);
        }


        public async Task SendNotificationAboutUploadedContent(string cohealerAccountId,
            IEnumerable<string> participantUserIds, string fileName, string contributionName,string downloadLink= "", string redirectLink = "")
        {
            var cohealerUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == cohealerAccountId);

            var clientUsers = (await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => participantUserIds.Contains(u.Id))).ToList();

            var clientAccountsIds = clientUsers.Select(u => u.AccountId).ToList();

            var accounts = (await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(a => clientAccountsIds.Contains(a.Id))).ToList();

            var filteredAccounts = accounts.Where(IsEmailNotificationEnabled);

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.UploadedFileToContribution);
            if (string.IsNullOrEmpty(redirectLink))
            {
                redirectLink = _loginLink;
            }

            foreach (var account in filteredAccounts)
            {
                var user = clientUsers.FirstOrDefault(u => u.AccountId == account.Id);

                var personalizedHtmlTemplate = emailHtmlTemplate
                    .Replace("{clientName}", $"{user?.FirstName}")
                    .Replace("{contributionName}", contributionName)
                    .Replace("{fileName}", fileName)
                    .Replace("{downloadLink}", downloadLink)
                    .Replace("{loginLink}", redirectLink)
                    .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink)
                    .Replace("{cohealerName}", $"{cohealerUser.FirstName} {cohealerUser.LastName}");

                await _emailService.SendAsync(account.Email, $"{cohealerUser.FirstName} {cohealerUser.LastName} shared a file with you",
                    personalizedHtmlTemplate);
            }
        }

        public async Task SendNotificationAboutNewRecording(string roomId, List<string> participantUserIds,
            string fileName, ContributionBaseViewModel model, string sessionTimeId)
        {
            var sessionId = "";
            
            var contributionbase = _mapper.Map<ContributionBase>(model);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contributionbase.UserId);
            if (contributionbase is SessionBasedContribution sessionbBaseContribution)
            {
                if (!string.IsNullOrEmpty(sessionTimeId))
                {
                    var session = sessionbBaseContribution.GetSessionBySessionTimeId(sessionTimeId);
                    if (session != null)
                        sessionId = session.Id;
                }else if (!string.IsNullOrEmpty(roomId))
                {
                    var session = sessionbBaseContribution.GetSessionByRoomId(roomId);
                    if (session != null)
                        sessionId = session.Id;
                }
            }

            var clientUsers = (await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => participantUserIds.Contains(u.Id))).ToList();

            var clientAccountsIds = clientUsers.Select(u => u.AccountId).ToList();

            var accounts = (await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(a => clientAccountsIds.Contains(a.Id))).ToList();

            var startRecordingTime = model.RecordingInfos.FirstOrDefault(info => info.RoomId == roomId)?.DateCreated;

            var filteredAccounts = accounts.Where(IsEmailNotificationEnabled);
            var subject = "Session Recording Available";
            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.NewRecordingsAvailable);
            (string template, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.NewRecordingsAvailable), emailHtmlTemplate, contributionbase, authoruser);

            emailHtmlTemplate = template;
            subject = updatedSubject;

            foreach (var account in filteredAccounts)
            {
                var user = clientUsers.FirstOrDefault(u => u.AccountId == account.Id);

                var recordingTime = !string.IsNullOrEmpty(user?.TimeZoneId) && startRecordingTime != null
                    ? DateTimeHelper.GetZonedDateTimeFromUtc((DateTime)startRecordingTime, user?.TimeZoneId)
                    : startRecordingTime;

                var personalizedHtmlTemplate = emailHtmlTemplate
                    .Replace("{receiverName}", $"{user?.FirstName}")
                    .Replace("{startRecordingTime}", recordingTime.ToString())
                    .Replace("{userTimezone}", user?.TimeZoneId)
                    .Replace("{contributionName}", model.Title)
                    .Replace("{fileName}", fileName)
                    .Replace("{loginLink}", $"{_urlSettings.WebAppUrl}/contribution-view/{model.Id}/sessions/{sessionId}?isPurchased=true")
                    .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);

                 subject = subject
                    .Replace("{receiverName}", $"{user?.FirstName}")
                    .Replace("{startRecordingTime}", recordingTime.ToString())
                    .Replace("{userTimezone}", user?.TimeZoneId)
                    .Replace("{contributionName}", model.Title)
                    .Replace("{fileName}", fileName)
                    .Replace("{loginLink}", $"{_urlSettings.WebAppUrl}/contribution-view/{model.Id}/sessions/{sessionId}?isPurchased=true")
                    .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);

                await _emailService.SendAsync(account.Email, subject, personalizedHtmlTemplate);
            }
        }

        public async Task SendEmailConfirmationLink(string accountEmail, string emailConfirmationToken, bool isCohealer)
        {
            var uriBuilder = new UriBuilder(_emailVerificationLink);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["email"] = accountEmail;
            query["token"] = emailConfirmationToken;
            uriBuilder.Query = query.ToString();

            string emailHtmlTemplate;

            if (isCohealer)
            {
                emailHtmlTemplate =
                    await GetTemplateContent(Constants.TemplatesPaths.Account.CohealerEmailConfirmation);
            }
            else
            {
                emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.ClientEmailConfirmation);
            }

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{verifyEmailLink}", uriBuilder.ToString()
                    .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                    .Replace("{year}", DateTime.UtcNow.Year.ToString()));

            await _emailService.SendAsync(accountEmail, "Welcome to Cohere. Please Verify Your Email",
                personalizedHtmlTemplate);
        }

        public async Task SendPasswordResetLink(string accountId, string accountEmail, string passwordRestorationToken)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var uriBuilder = new UriBuilder(_passwordRestorationRedirectUrl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["email"] = accountEmail;
            query["token"] = passwordRestorationToken;
            uriBuilder.Query = query.ToString();

            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.PasswordResetEmail);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{userFirstName}", user.FirstName)
                .Replace("{passwordResetLink}", uriBuilder.ToString())
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(accountEmail, "Reset Your Password", personalizedHtmlTemplate);
        }

        public async Task SendTransferMoneyNotification(string userFirstName, string email,
            string lastFourDigitsOfBankAccount)
        {
            var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == email);
            if (!IsEmailNotificationEnabled(cohealerAccount))
            {
                return;
            }

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.TransferMoneyNotification);

            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{userFirstName}", userFirstName)
                .Replace("{lastFourDigits}", lastFourDigitsOfBankAccount)
                .Replace("{loginLink}", _loginLink)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(email, "Money Is Being Transferred", personalizedHtmlTemplate);
        }

        public async Task SendContributionStatusNotificationToAuthor(ContributionBase contribution)
        {
            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            var cohealerAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == cohealerUser.AccountId);

            if (!IsEmailNotificationEnabled(cohealerAccount))
            {
                return;
            }

            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.StatusChanged);
            emailHtmlTemplate = emailHtmlTemplate
                .Replace("{loginLink}", _loginLink)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{year}", DateTime.UtcNow.Year.ToString());

            string largeText;
            string smallText;
            string personalizedHtmlTemplate;

            switch (contribution.Status)
            {
                case (ContributionStatuses.InReview):
                    {
                        largeText = "Your Contribution Is Pending Approval";
                        smallText =
                            $"{cohealerUser.FirstName}, thank you for submitting your Contribution!<br>It’s currently pending review. As soon as it’s approved, you will be ready to offer your services on Cohere!";

                        personalizedHtmlTemplate = emailHtmlTemplate
                            .Replace("{largeText}", largeText)
                            .Replace("{smallText}", smallText);
                        break;
                    }
                case (ContributionStatuses.ChangeRequired):
                    {
                        var reason = "Not provided";
                        if (contribution.AdminReviewNotes.Count > 0)
                        {
                            var adminReviewNote = contribution.AdminReviewNotes.OrderBy(n => n.DateUtc).Last();
                            reason = adminReviewNote.Description ?? reason;
                        }

                        largeText = "Your Contribution Needs Changes";
                        smallText =
                            $"{cohealerUser.FirstName}, {contribution.Title} was reviewed and needs a few items updated before it can be approved.<br>Reason: {reason}.<br>Please log in and update so we can approve your Contribution!";


                        personalizedHtmlTemplate = emailHtmlTemplate
                            .Replace("{largeText}", largeText)
                            .Replace("{smallText}", smallText);
                        break;
                    }
                case (ContributionStatuses.Rejected):
                    {
                        var reason = "Not provided";
                        if (contribution.AdminReviewNotes.Count > 0)
                        {
                            var adminReviewNote = contribution.AdminReviewNotes.OrderBy(n => n.DateUtc).Last();
                            reason = adminReviewNote.Description ?? reason;
                        }

                        largeText = "Your Contribution Was Declined";
                        smallText =
                            $"{cohealerUser.FirstName}, {contribution.Title} was reviewed and found to not meet our policy requirements and standards.<br>Unfortunately at this time you will not be able to deliver this Contribution on Cohere.<br>Reason: {reason}.<br>We encourage you to review the terms and conditions on Cohere and submit another Contribution.";

                        personalizedHtmlTemplate = emailHtmlTemplate
                            .Replace("{largeText}", largeText)
                            .Replace("{smallText}", smallText);
                        break;
                    }
                case (ContributionStatuses.Approved):
                    {
                        largeText = "Your Contribution Was Approved";
                        smallText =
                            $"Congrats {cohealerUser.FirstName}, {contribution.Title} was approved and is ready for business on Cohere.";

                        personalizedHtmlTemplate = emailHtmlTemplate
                            .Replace("{largeText}", largeText)
                            .Replace("{smallText}", smallText);


                        if (contribution is SessionBasedContribution existedContribution)
                        {
                            if (existedContribution.Sessions.All(x => x.IsPrerecorded))
                            {
                                break;
                            }

                            try
                            {
                                var locationUrl = existedContribution.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(existedContribution.Id));
                                var coachAndPartnersUserIds = new Dictionary<string, bool> { { cohealerUser.Id, true } };
                                foreach (var partner in contribution.Partners.Where(e => e.IsAssigned))
                                {
                                    coachAndPartnersUserIds.Add(partner.UserId, false);
                                }
                                var sessionTimes = existedContribution.Sessions?.Where(x => !x.IsPrerecorded).SelectMany(
                                    e => e.SessionTimes
                                        .Select(s => new SessionTimeToSession
                                        {
                                            Session = e,
                                            SessionTime = s,
                                            ContributionName = existedContribution.Title,
                                            CreatedDateTime = existedContribution.CreateTime
                                        }));

                                await SendLiveCourseWasUpdatedNotificationAsync(contribution.Title, coachAndPartnersUserIds,
                                    locationUrl, sessionTimes.ToList(),contribution.UserId, contribution.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "error during sending contribution events email");
                            }
                        }

                        break;
                    }
                case (ContributionStatuses.InSandbox):
                    {
                        largeText = "Please Verify Email And Bank Account";
                        smallText =
                            $"{cohealerUser.FirstName}, thank you for submitting your Contribution!<br>Before it can be approved we need you to please verify your email and add bank account information.<br> Please click the below link to verify email. Thank you!";

                        personalizedHtmlTemplate = emailHtmlTemplate
                            .Replace("{largeText}", smallText)
                            .Replace("{smallText}", largeText);
                        break;
                    }
                default:
                    return;
            }

            await _emailService.SendAsync(cohealerAccount.Email, largeText, personalizedHtmlTemplate);
        }

        public async Task SendEmailAboutInReviewToAdmins(ContributionBase contribution)
        {
            var adminAccounts = await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(x => x.Roles.Contains(Roles.Admin) || x.Roles.Contains(Roles.SuperAdmin));

            var adminAccountsWithEmailNotificationsEnabled = adminAccounts.Where(IsEmailNotificationEnabled).ToList();

            if (!adminAccountsWithEmailNotificationsEnabled.Any())
            {
                return;
            }

            var adminAddresses = adminAccountsWithEmailNotificationsEnabled.Select(x => x.Email).ToList();

            if (adminAddresses.Count > 0)
            {
                // handled in active campaign
                //var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.ReadyForReview);
                //emailHtmlTemplate = emailHtmlTemplate
                //    .Replace("{contributionName}", contribution.Title)
                //    .Replace("{loginLink}", _loginLink)
                //    .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                //    .Replace("{year}", DateTime.UtcNow.Year.ToString());

                //await _emailService.SendAsync(adminAddresses, "Contribution Is Pending Approval", emailHtmlTemplate);
            }
        }

        public async Task SendSessionReminders(DateTime dateTimeStart, DateTime dateTimeEnd, bool sendClientsOnly)
        {
            var partnerCoachSesionInfoFroReminders =
                await GetPartnerCoachSessionInfos(dateTimeStart, dateTimeEnd);

            var clientSessionInfoForReminders =
                await GetSessionReminderInfos(dateTimeStart, dateTimeEnd);
            clientSessionInfoForReminders.AddRange(partnerCoachSesionInfoFroReminders);
            if (clientSessionInfoForReminders.Count > 0)
            {
                await SendSessionReminders(clientSessionInfoForReminders, sendClientsOnly);
            }
        }

        #region Nylas API functions

        public async Task<NylasEventCreation> CreateorUpdateCalendarEvent(CalendarEvent calEvent, string clientid, NylasAccount nylasAccount, BookedTimeToAvailabilityTime bookedTimeToAvailabilityTime, bool isUpdate=false, string EventId = "")
        {
            string creatOrUpdateLink = "https://api.nylas.com/events?notify_participants=true";
            if (isUpdate==true && EventId!="")
            {
                creatOrUpdateLink = "https://api.nylas.com/events/" + EventId + "?notify_participants=true";
            }

            User _client =await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.Id == clientid);
            Account account =await _unitOfWork.GetGenericRepositoryAsync<Account>().GetOne(x => x.Id == _client.AccountId);
            List<Participants> participants = new List<Participants>()
            {
                new Participants(){ name = _client.FirstName +" "+_client.LastName , email =account.Email},
            };
            //Static Time  ZOne List
            List<string> staticTimeZones = new List<string> { "Canada/Atlantic",
            "Canada/Central" ,
            "Canada/Eastern",
            "Canada/Mountain",
            "Canada/Newfoundland",
            "Canada/Pacific",
            "Canada/Saskatchewan",
            "Canada/Yukon"};
            if (staticTimeZones.Contains(_client.TimeZoneId))
            {
                _client.TimeZoneId = "America/Vancouver";
            }
            When when = new When()
            {
                start_time = ((DateTimeOffset)bookedTimeToAvailabilityTime.BookedTime.StartTime).ToUnixTimeSeconds(),
                end_time = ((DateTimeOffset)bookedTimeToAvailabilityTime.BookedTime.EndTime).ToUnixTimeSeconds(),
                start_timezone = _client.TimeZoneId,
                end_timezone = _client.TimeZoneId
            };
            NylasEventCreation cohereDTO = new NylasEventCreation()
            {
                title = calEvent.Summary,
                calendar_id = nylasAccount.CalendarId,
                status = calEvent.Status,
                busy = true,
                read_only = true,
                participants = participants,
                description = calEvent.Description,
                when = when,
                location = calEvent.Location
            };
            // install package named "Nancy"
            string body = new JavaScriptSerializer().Serialize(cohereDTO);
            var client = new RestClient(creatOrUpdateLink);
            var request = new RestRequest();
            if(isUpdate==true && !string.IsNullOrEmpty(EventId))
            {
                request.Method= Method.PUT;
            }
            else
            {
                request.Method = Method.POST;
            }
            
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer "+nylasAccount.AccessToken);
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteAsync(request);
            NylasEventCreation eventResponse = JsonConvert.DeserializeObject<NylasEventCreation>(response.Content);          // use this to map all response content
            return eventResponse;
        }

        public async Task<NylasEventCreation> CreateorUpdateCalendarEventForSessionBase(CalendarEvent calEvent, List<string> clientids, NylasAccount nylasAccount, SessionTimeToSession sessionTimeToSession, bool isUpdate = false, string EventId = "")
        {
            string creatOrUpdateLink = "https://api.nylas.com/events?notify_participants=true";
            if (isUpdate==true && EventId!="")
            {
                creatOrUpdateLink = "https://api.nylas.com/events/" + EventId + "?notify_participants=true";
            }

            List<Participants> participants = new List<Participants>();
            foreach (string participant in clientids)
            {
                User _client = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.Id == participant);
                Account account = await _unitOfWork.GetGenericRepositoryAsync<Account>().GetOne(x => x.Id == _client.AccountId);
                participants.Add(new Participants() { name = _client.FirstName + " " + _client.LastName, email = account.Email });
            }
            User _firstclient = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.Id == clientids.FirstOrDefault());
            //Static Time  ZOne List
            //List<string> staticTimeZones = new List<string> { "Canada/Atlantic",
            //"Canada/Central" ,
            //"Canada/Eastern",
            //"Canada/Mountain",
            //"Canada/Newfoundland",
            //"Canada/Pacific",
            //"Canada/Saskatchewan",
            //"Canada/Yukon"};
            //if (staticTimeZones.Contains(_client.TimeZoneId))
            //{
            //    _client.TimeZoneId = "America/Vancouver";
            //}
            When when = new When()
            {
                start_time = ((DateTimeOffset)sessionTimeToSession.SessionTime.StartTime).ToUnixTimeSeconds(),
                end_time = ((DateTimeOffset)sessionTimeToSession.SessionTime.EndTime).ToUnixTimeSeconds(),
                start_timezone = _firstclient.TimeZoneId,
                end_timezone = _firstclient.TimeZoneId
            };
            NylasEventCreation cohereDTO = new NylasEventCreation()
            {
                title = calEvent.Summary,
                calendar_id = nylasAccount.CalendarId,
                status = calEvent.Status,
                busy = true,
                read_only = true,
                participants = participants,
                description = calEvent.Description,
                when = when,
                location = calEvent.Location
            };
            // install package named "Nancy"
            string body = new JavaScriptSerializer().Serialize(cohereDTO);
            var client = new RestClient(creatOrUpdateLink);
            var request = new RestRequest();
            if(isUpdate==true && !string.IsNullOrEmpty(EventId))
            {
                request.Method= Method.PUT;
            }
            else
            {
                request.Method = Method.POST;
            }
            
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer "+nylasAccount.AccessToken);
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteAsync(request);
            NylasEventCreation eventResponse = JsonConvert.DeserializeObject<NylasEventCreation>(response.Content);          // use this to map all response content
            return eventResponse;
        }

        public async Task<bool> DeleteCalendarEventForSessionBase(SessionBasedContribution updatedCourse, string sessionTimeId, string participantAccountId, string eventId = "")
        {
            var coach = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == updatedCourse.UserId);
            var clientobj = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == participantAccountId);
            var sessionTime = updatedCourse.Sessions.SelectMany(x => x.SessionTimes).Where(x => x.Id == sessionTimeId).FirstOrDefault();
            if (sessionTime == null && !string.IsNullOrEmpty(eventId))
            {
                NylasAccount NylasAccount = null;
                if (!string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                    NylasAccount = await _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == coach.AccountId && n.EmailAddress.ToLower() == updatedCourse.ExternalCalendarEmail.ToLower());
                if (NylasAccount != null && !string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                {
                    string creatOrUpdateLink = "https://api.nylas.com/events/" + eventId + "?notify_participants=true";
                    var client = new RestClient(creatOrUpdateLink);
                    var request = new RestRequest();

                    request.Method = Method.DELETE;


                    request.AddHeader("Accept", "application/json");
                    request.AddHeader("Content-Type", "application/json");
                    request.AddHeader("Authorization", "Bearer " + NylasAccount.AccessToken);

                    IRestResponse response = await client.ExecuteAsync(request);
                    if (response.ResponseStatus == ResponseStatus.Completed)
                    {
                        return true;
                    }
                }

            }
            EventInfo eventInfo = sessionTime.EventInfos.Where(x => x.ParticipantId == clientobj.Id).FirstOrDefault();
            if (eventInfo != null)
            {
                NylasAccount NylasAccount = null;
                if (!string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                    NylasAccount = await _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == coach.AccountId && n.EmailAddress.ToLower() == updatedCourse.ExternalCalendarEmail.ToLower());
                if (NylasAccount != null && !string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                {
                    if (eventInfo.CalendarId == NylasAccount.CalendarId)
                    {
                        var sessionTimes = updatedCourse.GetSessionTimes($"{coach.FirstName} {coach.LastName}");
                        SessionTimeToSession item = sessionTimes.Values.Where(x => x.SessionTime.Id == sessionTimeId).FirstOrDefault();
                        if (item != null)
                            {
                                var ids = item.SessionTime.ParticipantsIds.Distinct().ToList();
                                if (ids.Count() == 0)
                                {
                                    string creatOrUpdateLink = "https://api.nylas.com/events/" + eventInfo.CalendarEventID + "?notify_participants=true";
                                    var client = new RestClient(creatOrUpdateLink);
                                    var request = new RestRequest();

                                    request.Method = Method.DELETE;


                                    request.AddHeader("Accept", "application/json");
                                    request.AddHeader("Content-Type", "application/json");
                                    request.AddHeader("Authorization", "Bearer " + NylasAccount.AccessToken);

                                    IRestResponse response = await client.ExecuteAsync(request);
                                    if (response.ResponseStatus == ResponseStatus.Completed)
                                    {
                                        sessionTime.EventInfos.Remove(eventInfo);
                                        await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(updatedCourse.Id, updatedCourse);
                                        return true;
                                    }
                                }
                                else
                                {
                                    CalendarEvent calevent = _mapper.Map<CalendarEvent>(item);
                                    var locationUrl = updatedCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedCourse.Id));
                                    calevent.Location = locationUrl;
                                    calevent.Description = updatedCourse.CustomInvitationBody;
                                    NylasEventCreation eventResponse = await CreateorUpdateCalendarEventForSessionBase(calevent, ids.ToList(), NylasAccount, item, true, eventInfo.CalendarEventID);
                                    if (eventResponse.id != null)
                                    {
                                        sessionTime.EventInfos.Remove(eventInfo);
                                        await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(updatedCourse.Id, updatedCourse);
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                            }

                        
                    }
                }
            }

            return false;
        }

        #endregion

        public async Task SendSessionReminders(List<SessionInfoForReminderViewModel> sessionInfosForReminders, bool sendClientsOnly)
        {
            var userIdsFromSessionInfos = sessionInfosForReminders.Select(si => si.ClientUserId);
            var usersToRemind = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => userIdsFromSessionInfos.Contains(u.Id));
            var usersToRemindList = usersToRemind.ToList();

            var usersAccountIds = usersToRemindList.Select(u => u.AccountId);
            var userAccountsToRemind =
                await _unitOfWork.GetRepositoryAsync<Account>().Get(a => usersAccountIds.Contains(a.Id));

            var clientNecessaryInfos = from user in usersToRemindList
                                       join account in userAccountsToRemind on user.AccountId equals account.Id
                                       select new
                                       {
                                           ClientUserId = user.Id,
                                           ClientEmail = account.Email,
                                           ClientFirstName = user.FirstName,
                                           user.TimeZoneId,
                                           IsClientEmailNotificationsEnabled = IsEmailNotificationEnabled(account)
                                       };

            var authorIdsFromSessionInfos = sessionInfosForReminders.Select(si => si.AuthorUserId);
            var authorUsers = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => authorIdsFromSessionInfos.Contains(u.Id));
            var authorUsersList = authorUsers.ToList();
            var authorAccountIds = authorUsersList.Select(a => a.AccountId);
            var authorAccounts =
                await _unitOfWork.GetRepositoryAsync<Account>().Get(a => authorAccountIds.Contains(a.Id));

            var authorNecessaryInfos = from authorUser in authorUsersList
                                       join authorAccount in authorAccounts on authorUser.AccountId equals authorAccount.Id
                                       select new
                                       {
                                           AuthorUserId = authorUser.Id,
                                           AuthorEmail = authorAccount.Email,
                                           AuthorFirstName = authorUser.FirstName,
                                           authorUser.TimeZoneId,
                                           IsAuthorEmailNotificationsEnabled = IsEmailNotificationEnabled(authorAccount)
                                       };

            var timezones = await _timeZoneService.GetAll();

            var sessionReminders = new List<SessionReminderViewModel>();

            sessionInfosForReminders.ForEach(si =>
            {
                var authorInfo = authorNecessaryInfos.FirstOrDefault(i => i.AuthorUserId == si.AuthorUserId);
                var clientInfo = clientNecessaryInfos.FirstOrDefault(ui => ui.ClientUserId == si.ClientUserId);
                if (authorInfo != null && clientInfo != null)
                {
                    sessionReminders.Add(
                        new SessionReminderViewModel
                        {
                            AuthorUserId = authorInfo.AuthorUserId,
                            AuthorFirstName = authorInfo.AuthorFirstName,
                            AuthorEmail = authorInfo.AuthorEmail,
                            AuthorTimeZoneId = authorInfo.TimeZoneId,
                            AuthorClassStartTimeZoned =
                                DateTimeHelper.GetZonedDateTimeFromUtc(si.ClassStartTimeUtc, authorInfo.TimeZoneId),
                            IsAuthorEmailNotificationsEnabled = authorInfo.IsAuthorEmailNotificationsEnabled,
                            ClientUserId = clientInfo.ClientUserId,
                            ClientFirstName = clientInfo.ClientFirstName,
                            ClientEmail = clientInfo.ClientEmail,
                            ClientTimeZoneId = clientInfo.TimeZoneId,
                            ClientClassStartTimeZoned =
                                DateTimeHelper.GetZonedDateTimeFromUtc(si.ClassStartTimeUtc, clientInfo.TimeZoneId),
                            IsClientEmailNotificationsEnabled = clientInfo.IsClientEmailNotificationsEnabled,
                            ContributionId = si.ContributionId,
                            ContributionTitle = si.ContributionTitle,
                            ClassId = si.ClassId
                        });
                }
            });

            var groupedClientModels = sessionReminders.GroupBy(m => m.ClientEmail);

            var clientEmailHtmlTemplate = "";
            var EmailType = "";
            if (sendClientsOnly)
            {
                EmailType = nameof(Constants.TemplatesPaths.Contribution.ClientSessionOneHourReminder);
                clientEmailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientSessionOneHourReminder);
            }
            else
            {
                EmailType = nameof(Constants.TemplatesPaths.Contribution.ClientSessionReminder);
                clientEmailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientSessionReminder);
            }
            foreach (var groupedModel in groupedClientModels)
            {
                var clientEmail = groupedModel.Key;
                foreach (var model in groupedModel)
                {
                    var contributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                                                .GetOne(c => c.Id == model.ContributionId);
                    if (contributionBase != null)
                    {
                        if (contributionBase.Partners.Select(x => x.UserId).Contains(model.AuthorUserId))
                        {
                            continue;
                        }
                    }
                    //Send Push Notification and Email notifications to clients
                    try
                    {
                        var subject = "Upcoming Session Reminder";
                        var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.Id == model.AuthorUserId);
                        (string template, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, EmailType, clientEmailHtmlTemplate, contributionBase, authorUser);

                        clientEmailHtmlTemplate = template;
                        subject = updatedSubject;

                        var editedClientEmailHtmlTemplate = clientEmailHtmlTemplate
                                .Replace("{clientFirstName}", model.ClientFirstName)
                               .Replace("{contributionName}", model.ContributionTitle)
                               .Replace("{cohealerFirstName}", model.AuthorFirstName)
                               .Replace("{date}", model.ClientClassStartTimeZoned.ToString("MMMM dd"))
                               .Replace("{time}", model.ClientClassStartTimeZoned.ToString("hh:mm tt"))
                                .Replace("{timezone}", timezones.FirstOrDefault(x => x.CountryName == model.ClientTimeZoneId)?.Name)
                               .Replace("{loginLink}", _loginLink)
                               .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                               .Replace("{year}", DateTime.UtcNow.Year.ToString());
                        
                         subject = subject
                                .Replace("{clientFirstName}", model.ClientFirstName)
                               .Replace("{contributionName}", model.ContributionTitle)
                               .Replace("{cohealerFirstName}", model.AuthorFirstName)
                               .Replace("{date}", model.ClientClassStartTimeZoned.ToString("MMMM dd"))
                               .Replace("{time}", model.ClientClassStartTimeZoned.ToString("hh:mm tt"))
                                .Replace("{timezone}", timezones.FirstOrDefault(x => x.CountryName == model.ClientTimeZoneId)?.Name)
                               .Replace("{loginLink}", _loginLink)
                               .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                               .Replace("{year}", DateTime.UtcNow.Year.ToString());

                        await _emailService.SendAsync(clientEmail, subject , editedClientEmailHtmlTemplate);

                        await _fcmService.SendHourlyReminderPushNotification(clientEmail, model.ClassId, model.ContributionId,sendClientsOnly);
                       
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erorr sending Upcoming Session Reminder Email to clients in SendSessionReminders");
                    } 
                }
            }
            if (sendClientsOnly)
            {
                return;
            }
            var groupedCohealerModels = sessionReminders.GroupBy(m => m.AuthorEmail);

            var cohealerEmailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.CohealerSessionReminder);

            foreach (var groupedModel in groupedCohealerModels)
            {
                var authorEmail = groupedModel.Key;
                foreach (var model in groupedModel.ToList().Distinct(new SessionReminderViewModel()))
                {
                    //Send Push Notification and Email notifications to coaches
                    try
                    {
                        var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == model.ContributionId);
                        var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.Id == model.AuthorUserId);
                        var subject = "Upcoming Session Reminder";
                        (string template, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.CohealerSessionReminder), cohealerEmailHtmlTemplate,contribution, authorUser);

                        cohealerEmailHtmlTemplate = template;
                        subject = updatedSubject;

                        var cohealerEditedHtmlTemplate = cohealerEmailHtmlTemplate
                       .Replace("{cohealerFirstName}", model.AuthorFirstName)
                       .Replace("{contributionName}", model.ContributionTitle)
                       .Replace("{date}", model.AuthorClassStartTimeZoned.ToString("MMMM dd"))
                       .Replace("{time}", model.AuthorClassStartTimeZoned.ToString("hh:mm tt"))
                       .Replace("{timezone}", timezones.FirstOrDefault(x => x.CountryName == model.AuthorTimeZoneId)?.Name)
                       .Replace("{loginLink}", _loginLink)
                       .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                       .Replace("{year}", DateTime.UtcNow.Year.ToString());
                        
                        subject = subject
                       .Replace("{cohealerFirstName}", model.AuthorFirstName)
                       .Replace("{contributionName}", model.ContributionTitle)
                       .Replace("{date}", model.AuthorClassStartTimeZoned.ToString("MMMM dd"))
                       .Replace("{time}", model.AuthorClassStartTimeZoned.ToString("hh:mm tt"))
                       .Replace("{timezone}", timezones.FirstOrDefault(x => x.CountryName == model.AuthorTimeZoneId)?.Name)
                       .Replace("{loginLink}", _loginLink)
                       .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                       .Replace("{year}", DateTime.UtcNow.Year.ToString());

                        await _emailService.SendAsync(authorEmail, subject, cohealerEditedHtmlTemplate);

                        await _fcmService.SendHourlyReminderPushNotification(authorEmail, model.ClassId, model.ContributionId, sendClientsOnly);

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erorr sending Upcoming Session Reminder Email to coaches in SendSessionReminders");
                    }
                }
            }
        }

        public async Task SendUnreadConversationsNotification(HashSet<string> emailsToSendNotifications)
        {
            var userAccountsToSend = await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(a => emailsToSendNotifications.Contains(a.Email));
            var userAccountsToSendList = userAccountsToSend.Where(IsEmailNotificationEnabled).ToList();
            var accountIdsToSendUnreadGroup = userAccountsToSendList.Select(a => a.Id);

            var userInfosToSend = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => accountIdsToSendUnreadGroup.Contains(u.AccountId));

            var usersToSendModels = from user in userInfosToSend
                                    join account in userAccountsToSendList on user.AccountId equals account.Id
                                    select new { UserId = user.Id, account.Email, user.FirstName };

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Communication.UnreadConversationGeneral);

            foreach (var userToSendModel in usersToSendModels)
            {
                var editedHtmlTemplate = emailHtmlTemplate
                    .Replace("{firstName}", userToSendModel.FirstName)
                    .Replace("{loginLink}", $"{_urlSettings.WebAppUrl}/conversations/all")
                    .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                    .Replace("{year}", DateTime.UtcNow.Year.ToString());

                await _emailService.SendAsync(userToSendModel.Email, "You Have Unread Conversations",
                    editedHtmlTemplate);
            }
        }

        public async Task SendSessionRescheduledNotification(
            List<EditedBookingWithClientId> editedBookingsWithClientIds, string contributionAuthorFirstName)
        {
            var editedSessionsParticipantIds = editedBookingsWithClientIds.Select(i => i.ParticipantId);
            var participantsUserInfos = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => editedSessionsParticipantIds.Contains(u.Id));
            var participantsUserInfosList = participantsUserInfos.ToList();
            var participantsAccountIds = participantsUserInfosList.Select(u => u.AccountId);
            var participantsAccounts = await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(a => participantsAccountIds.Contains(a.Id));
            var participantsAccountsWithNotificationsEnabled = participantsAccounts.Where(IsEmailNotificationEnabled);
            var usersToSendModels = participantsAccountsWithNotificationsEnabled
                .Join(participantsUserInfosList, a => a.Id, u => u.AccountId, (a, u) =>
                    new
                    {
                        u.AccountId,
                        ClientId = u.Id,
                        ClientFirstName = u.FirstName,
                        ClientEmail = a.Email,
                        ClientTimeZoneId = u.TimeZoneId
                    })
                .Join(editedBookingsWithClientIds, i => i.ClientId, eBId => eBId.ParticipantId, (i, eBid) =>
                    new
                    {
                        i.ClientFirstName,
                        i.ClientEmail,
                        CohealerFirstName = contributionAuthorFirstName,
                        NewStartTime = DateTimeHelper.GetZonedDateTimeFromUtc(eBid.NewStartTime, i.ClientTimeZoneId),
                        OldStartTime = DateTimeHelper.GetZonedDateTimeFromUtc(eBid.OldStartTime, i.ClientTimeZoneId),
                        eBid.ContributionId,
                        eBid.ContributionTitle,
                        eBid.ClassGroupId,
                        eBid.ClassGroupName,
                        eBid.ClassId,
                        eBid.ParticipantId,
                        i.ClientTimeZoneId,
                        eBid.ContributionName,
                        eBid.UpdatedSessionName
                    });

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientBookedTimeEdited);

            foreach (var userToSendModel in usersToSendModels)
            {
                var editedHtmlTemplate = emailHtmlTemplate
                    .Replace("{clientFirstName}", userToSendModel.ClientFirstName)
                    .Replace("{cohealerFirstName}", userToSendModel.CohealerFirstName)
                     .Replace("{updatedSessionName}", userToSendModel.UpdatedSessionName)
                     .Replace("{contributionName}", userToSendModel.ContributionName)
                     .Replace("{olddate}", userToSendModel.OldStartTime.ToString("MMMM dd"))
                    .Replace("{oldtime}", userToSendModel.OldStartTime.ToString("hh:mm tt"))
                    .Replace("{date}", userToSendModel.NewStartTime.ToString("MMMM dd"))
                    .Replace("{time}", userToSendModel.NewStartTime.ToString("hh:mm tt"))
                    .Replace("{timezone}", GetFriendlyTimeZoneName(userToSendModel.ClientTimeZoneId))
                    .Replace("{loginLink}", _loginLink)
                    .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink)
                    .Replace("{year}", DateTime.UtcNow.Year.ToString());

                await _emailService.SendAsync(userToSendModel.ClientEmail, "Your Session Has Been Rescheduled",
                    editedHtmlTemplate);
            }
        }

        public async Task SendSessionDeletedNotification(List<DeletedBookingWithClientId> deletedBookingsWithClientIds,
            string authorFirstName)
        {
            var deletedSessionsParticipantIds = deletedBookingsWithClientIds.Select(i => i.ParticipantId);
            var participantsUserInfos = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => deletedSessionsParticipantIds.Contains(u.Id));
            var participantsUserInfosList = participantsUserInfos.ToList();
            var participantsAccountIds = participantsUserInfosList.Select(u => u.AccountId);
            var participantsAccounts = await _unitOfWork.GetRepositoryAsync<Account>()
                .Get(a => participantsAccountIds.Contains(a.Id));
            var participantsAccountsWithNotificationsEnabled = participantsAccounts.Where(IsEmailNotificationEnabled);
            var usersToSendModels = participantsAccountsWithNotificationsEnabled
                .Join(participantsUserInfosList, a => a.Id, u => u.AccountId, (a, u) =>
                    new
                    {
                        u.AccountId,
                        ClientId = u.Id,
                        ClientFirstName = u.FirstName,
                        ClientEmail = a.Email,
                        ClientTimeZoneId = u.TimeZoneId
                    })
                .Join(deletedBookingsWithClientIds, i => i.ClientId, eBId => eBId.ParticipantId, (i, eBid) =>
                    new
                    {
                        i.ClientFirstName,
                        i.ClientEmail,
                        CohealerFirstName = authorFirstName,
                        DeletedStartTime =
                            DateTimeHelper.GetZonedDateTimeFromUtc(eBid.DeletedStartTime, i.ClientTimeZoneId),
                        eBid.ContributionId,
                        eBid.ContributionTitle,
                        eBid.ClassGroupId,
                        eBid.ClassGroupName,
                        eBid.ClassId,
                        eBid.ParticipantId,
                        i.ClientTimeZoneId,
                        eBid.ContributionName
                    });

            var emailHtmlTemplate =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientBookedTimeDeleted);

            foreach (var userToSendModel in usersToSendModels)
            {
                var editedHtmlTemplate = emailHtmlTemplate
                    .Replace("{clientFirstName}", userToSendModel.ClientFirstName)
                     .Replace("{contributionName}", userToSendModel.ContributionName)
                    .Replace("{classGroupName}", userToSendModel.ClassGroupName)
                    .Replace("{cohealerFirstName}", userToSendModel.CohealerFirstName)
                    .Replace("{date}", userToSendModel.DeletedStartTime.ToString("MMMM dd"))
                    .Replace("{time}", userToSendModel.DeletedStartTime.ToString("hh:mm tt"))
                    .Replace("{timezone}", GetFriendlyTimeZoneName(userToSendModel.ClientTimeZoneId))
                    .Replace("{contributionTitle}", userToSendModel.ContributionTitle)
                    .Replace("{loginLink}", _loginLink)
                    .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                    .Replace("{year}", DateTime.UtcNow.Year.ToString());

                await _emailService.SendAsync(userToSendModel.ClientEmail, "Please Reschedule Your Session Time",
                    editedHtmlTemplate);
            }
        }

        public async Task SendInstructionsToNewCohealerAsync(string cohealerAccountId)
        {
            var cohealerUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(c => c.AccountId == cohealerAccountId);
            var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == cohealerAccountId);
            var emailHtmlTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.NewCohealerInstructions);
            var personalizedHtmlTemplate = emailHtmlTemplate
                .Replace("{CoachFirstName}", cohealerUser.FirstName)
                .Replace("{loginLink}", _loginLink)
                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);
            await _emailService.SendAsync(clientAccount.Email, "Finish Getting Setup", personalizedHtmlTemplate);
        }

        public async Task SendEmailPartnerCoachInvite(string email, ContributionBase contribution,
            string assignActionUrl)
        {
            var user = (await _unitOfWork.GetRepositoryAsync<User>().Get(x => x.Id == contribution.UserId))
                .FirstOrDefault();

            var subject = "Join the team";

            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.SendEmailPartnerCoachInvite);
            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.SendEmailPartnerCoachInvite), template, contribution, user);

            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{contributionName}", contribution.Title)
                .Replace("{ownerFirstName}", user.FirstName)
                .Replace("{registerLink}", _signUpUrl)
                .Replace("{assignLink}", assignActionUrl);

                subject = subject
               .Replace("{contributionName}", contribution.Title)
               .Replace("{ownerFirstName}", user.FirstName)
               .Replace("{registerLink}", _signUpUrl)
               .Replace("{assignLink}", assignActionUrl);

            await _emailService.SendAsync(email, subject, finalTemplate);
        }

        public async Task SendEmailCohealerInstructionGuide(string cohealerEmail, string cohealerFirstName)
        {
            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.CreateContributionGuide);

            var finalTemplate = template
                .Replace("{cohealerFirstName}", cohealerFirstName)
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendAsync(cohealerEmail, "How to Create a Group Coaching Service", finalTemplate);
        }

        public async Task SendEmailCohealerOneToOneInstructionGuide(string cohealerEmail, string cohealerFirstName)
        {
            string template =
                await GetTemplateContent(Constants.TemplatesPaths.Contribution.CreateOneToOneContributionGuide);

            var finalTemplate = template
                .Replace("{cohealerFirstName}", cohealerFirstName)
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendAsync(cohealerEmail, "How to Create a 1:1 Coaching Service", finalTemplate);
        }

        public async Task SendEmailCohealerShareContributionGuide(ContributionBase contribution)
        {
            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            if (!cohealerUser.FirstContributionGuideSent)
            {
                var cohealerAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                    .GetOne(x => x.Id == cohealerUser.AccountId);

                var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.ShareContributionGuide);

                var finalTemplate = template
                    .Replace("{cohealerFirstName}", cohealerUser.FirstName)
                    .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink)
                    .Replace("{loginLink}", _loginLink);

                await _emailService.SendAsync(cohealerAccount.Email,
                    "How to Invite Your Clients to Purchase Your Services", finalTemplate);
                cohealerUser.FirstContributionGuideSent = true;
                await _unitOfWork.GetRepositoryAsync<User>().Update(cohealerUser.Id, cohealerUser);
            }
        }

        public string GetTemplateFullPath(string templatePath)
        {
            return Path.Combine(AppContext.BaseDirectory, templatePath);
        }

        public async Task<string> GetTemplateContent(string templatePath)
        {
            return await File.ReadAllTextAsync(GetTemplateFullPath(templatePath));
        }

        public async Task SendLiveCourseWasUpdatedNotificationAsync(string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserId, string locationUrl, List<SessionTimeToSession> createdEvents, string contributorId,string contributionId, bool sendIcalAttachment = true)
        {
            await SendLiveCourseWasUpdatedNotificationAsync(liveCourseTitle, coachAndPartnerUserId, locationUrl,
                new EventDiff() { CreatedEvents = createdEvents },contributorId, contributionId,sendIcalAttachment);
        }

        public async Task NotifyTaggedUsers(UserTaggedNotificationViewModel model)
        {
            if (!string.IsNullOrEmpty(model.PostId))
            {
                var post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(u => u.Id == model.PostId);

                if (post != null)
                {
                    if (!post.IsScheduled)
                    {
                        var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == post.ContributionId);
                        var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
                        var subject = "You were tagged in {contributionName}";
                        var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.UserWasTaggedNotification);
                        (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.UserWasTaggedNotification), template, contribution, authoruser);

                        template = updatedTemplate;
                        subject = updatedSubject;

                        var usersToNotify =
                            (await _unitOfWork.GetRepositoryAsync<User>().Get(u => model.MentionedUserIds.Contains(u.Id)))
                            .ToDictionary(x => x.AccountId);

                        var accountsToNotify =
                            await _unitOfWork.GetRepositoryAsync<Account>().Get(a => usersToNotify.Keys.Contains(a.Id));

                        foreach (var account in accountsToNotify)
                        {
                            var finalTemplate = template
                                .Replace("{mentionAuthorUserName}", model.MentionAuthorUserName)
                                .Replace("{contributionName}", model.ContributionName)
                                .Replace("{replyLink}", model.ReplyLink)
                                .Replace("{mentionDate}", DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow,
                                    usersToNotify[account.Id].TimeZoneId).ToString())
                                .Replace("{message}", model.Message)
                                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);
                            
                                 subject = subject
                                .Replace("{mentionAuthorUserName}", model.MentionAuthorUserName)
                                .Replace("{contributionName}", model.ContributionName)
                                .Replace("{replyLink}", model.ReplyLink)
                                .Replace("{mentionDate}", DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow,
                                    usersToNotify[account.Id].TimeZoneId).ToString())
                                .Replace("{message}", model.Message)
                                .Replace("{unsubscribeEmailLink}", _unsubscribeEmailLink);

                            await _emailService.SendAsync(account.Email, subject,
                                finalTemplate);
                        }
                    }
                    else
                    {
                        post.ReplyLink = model.ReplyLink;
                        await _unitOfWork.GetRepositoryAsync<Post>().Update(post.Id, post);
                    }
                }
            }

            
        }

        private static object lockObject = new object();
        public async Task SendLiveCourseWasUpdatedNotificationAsync(
            string sourceAddress,
            string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserIds,
            string locationUrl,
            EventDiff eventDiff,string contributorId,string contributionId, bool sendIcalAttachment = true)
            {
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
                    var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId); 
                    foreach (var coachId in coachAndPartnerUserIds.Keys)
                    {
                        
                        (var cohealerEmail, var cohealerCommonName, var cohealerTimeZoneId) =
                            await GetCoachInvitationEmailInfo(coachId);

                        var attachments = _calendarSyncService.CreateICalFile(
                            cohealerCommonName,
                            cohealerEmail,
                            sourceAddress,
                            locationUrl,
                            eventDiff,
                            coachAndPartnerUserIds[coachId], contribution.CustomInvitationBody);

                        var sessionDetails = GetSessionDetails(eventDiff, cohealerTimeZoneId);
                        var subject = "Confirmed Session(s) for {contributionName}";
                        var template = "";
                        if (sendIcalAttachment)
                            {
                                template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                                .ContributionSessionsWasUpdatedNotification);
                            }
                        else
                            template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                            .ContributionSessionsWasUpdatedNotificationForNylas);
                
                (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.ContributionSessionsWasUpdatedNotification), template, contribution, authoruser);

                template = updatedTemplate;
                subject=updatedSubject;

                var finalTemplate = template
                            .Replace("{sessionsDetails}", sessionDetails)
                    .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                            .Replace("{contributionName}", liveCourseTitle)
                            .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                            .Replace("{loginLink}", _loginLink);

                    subject = subject
                            .Replace("{sessionsDetails}", sessionDetails)
                    .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                            .Replace("{contributionName}", liveCourseTitle)
                            .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                            .Replace("{loginLink}", _loginLink);

                        await _emailService.SendWithAttachmentsAsync(sourceAddress, cohealerEmail, subject, finalTemplate, attachments, sendIcalAttachment);

                    }
              
            }

        private dynamic GetBrandingColorsForEmails(ContributionBase contribution, User author)
        {
            Dictionary<string, string> LegacyColors = new Dictionary<string, string>();
            LegacyColors.Add("PrimaryColorCode", CohereLegacyColors.PrimaryColorCode);
            LegacyColors.Add("AccentColorCode", CohereLegacyColors.AccentColorCode);
            LegacyColors.Add("TertiaryColorCode", CohereLegacyColors.TertiaryColorCode);
            LegacyColors.Add("TextColorCode", CohereLegacyColors.TextColorCode);

            dynamic obj = new ExpandoObject();
            if (contribution.IsCustomBrandingColorsActive)
            {
                if (!contribution.BrandingColors.ContainsKey("TextColorCode"))
                {
                    contribution.BrandingColors.Add("TextColorCode", "Auto");
                }
                if (!author.BrandingColors.ContainsKey("TextColorCode"))
                {
                    author.BrandingColors.Add("TextColorCode", "Auto");
                }
                if (contribution?.BrandingColors == null || contribution?.BrandingColors.Count == 0 || (contribution.BrandingColors.Count == LegacyColors.Count && !contribution.BrandingColors.Except(LegacyColors).Any()))
                {
                    obj.PrimaryColorCode = author.BrandingColors["PrimaryColorCode"];
                    obj.AccentColorCode = author.BrandingColors["AccentColorCode"];
                    obj.TertiaryColorCode = author.BrandingColors["TertiaryColorCode"];
                    obj.TextColorCode = author.BrandingColors["TextColorCode"];
                }
                else
                {
                    obj.PrimaryColorCode =  contribution.BrandingColors["PrimaryColorCode"];
                    obj.AccentColorCode =   contribution.BrandingColors["AccentColorCode"];
                    obj.TertiaryColorCode = contribution.BrandingColors["TertiaryColorCode"];
                    obj.TextColorCode = contribution.BrandingColors["TextColorCode"];
                }
                 

            }else
            {
                obj.PrimaryColorCode = CohereLegacyColors.PrimaryColorCode;
                obj.AccentColorCode = CohereLegacyColors.AccentColorCode;
                obj.TertiaryColorCode = CohereLegacyColors.TertiaryColorCode;
                obj.TextColorCode = CohereLegacyColors.TextColorCode;
            }

            return obj;
        }

        private (string, string) GetUpdatedCustomHtmlForEmail(string subject, string emailType, string emailHtmlTemplate, ContributionBase contribution, User author)
        {
            try
            {
                var emailTemplate = _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(u => u.ContributionId == contribution.Id).Result;
                if (emailTemplate != null)
                {
                    var customTemplate = emailTemplate.CustomTemplates.Where(x => x.EmailType == emailType && x.IsEmailEnabled).FirstOrDefault();
                    if (customTemplate != null)
                    {
                        emailHtmlTemplate = customTemplate.EmailText;
                        subject = customTemplate.EmailSubject;

                        if (string.IsNullOrEmpty(emailHtmlTemplate))
                            emailHtmlTemplate = customTemplate.EmailText;
                        if (string.IsNullOrEmpty(subject))
                            subject = customTemplate.EmailSubject;

                        if (customTemplate.IsCustomBrandingColorsEnabled && emailHtmlTemplate!=null)
                        {
                            var customColors = GetBrandingColorsForEmails(contribution, author);
                            emailHtmlTemplate = emailHtmlTemplate
                            .Replace("#0b6481", customColors.AccentColorCode)
                            .Replace("#d1b989", customColors.PrimaryColorCode);
                        }
                        
                    }
                }
            }
            catch
            {

            }
           

            return (emailHtmlTemplate, subject);
        }

        private string GetSessionDetails(
            EventDiff eventDiff,
            string timeZoneId
        )
        {
            var orderedEvents = eventDiff.CreatedEvents.Concat(eventDiff.UpdatedEvents)
                .OrderBy(e => e.SessionTime.StartTime)
                .Select(e => GetDetailsForCreatedOrUpdatedEvent(e, timeZoneId));

            var items = string.Join(Environment.NewLine, orderedEvents);

            return $"<ul>{items}</ul>";
        }

        private string GetDetailsForCreatedOrUpdatedEvent(SessionTimeToSession @event, string timeZoneId)
        {
            var zonedTime = DateTimeHelper.GetZonedDateTimeFromUtc(@event.SessionTime.StartTime, timeZoneId);
            return
                $"<li>{@event.Session.Name} {@event.ClientName} is confirmed for {zonedTime.ToString("MM/dd")} at {zonedTime.ToString("hh:mmtt")} {{timeZoneFriendlyName}}</li>";
        }

        private string GetSessionDetails(List<BookedTimeToAvailabilityTime> createdEvents, string timeZoneId)
        {
            var orderedEvents = createdEvents.OrderBy(e => e.BookedTime.StartTime)
                .Select(e => GetDetailsForCreatedOrUpdatedEvent(e, timeZoneId));

            var items = string.Join(Environment.NewLine, orderedEvents);

            return $"<ul>{items}</ul>";
        }

        private string GetDetailsForCreatedOrUpdatedEvent(BookedTimeToAvailabilityTime @event, string timeZoneId)
        {
            var zonedTime = DateTimeHelper.GetZonedDateTimeFromUtc(@event.BookedTime.StartTime, timeZoneId);
            return
                $"<li>{@event.ContributionName} {@event.ClientName} is confirmed for {zonedTime.ToString("MM/dd")} at {zonedTime.ToString("hh:mmtt")} {{timeZoneFriendlyName}}</li>";
        }

        private async Task<(string email, string commonName, string userTimeZoneId)> GetClientInvitationEmailInfo(
            string userId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == user.AccountId);

            return (account.Email, $"{user.FirstName} {user.LastName}", user.TimeZoneId);
        }

        private async Task<(string email, string commonName, string userTimeZoneId)> GetCoachInvitationEmailInfo(
            string userId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == user.AccountId);

            return (account.Email, $"{user.FirstName} {user.LastName}", user.TimeZoneId);
        }

        public async Task SendLiveCourseWasUpdatedNotificationAsync(
            string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserIds,
            string locationUrl,
            EventDiff eventDiff, string contributorId,string contributionId, bool sendIcalAttachment = true)
        {
            await SendLiveCourseWasUpdatedNotificationAsync(_sessionNotificationSourceAddress, liveCourseTitle,
                coachAndPartnerUserIds, locationUrl, eventDiff , contributorId, contributionId,sendIcalAttachment);
        }

        public async Task SendLiveCouseBookSessionNotificationForClientAsync(string liveCourseTitle, string userId,
            string locationUrl, List<SessionTimeToSession> bookedEvents, string contributorId, string contributionId, bool sendIcalAttachment = true)
        {
            await SendLiveCourseWasUpdatedNotificationAsync(liveCourseTitle, new Dictionary<string, bool> { { userId, false } }, locationUrl,
                bookedEvents , contributorId, contributionId, sendIcalAttachment);
        }

        public async Task SendOneToOneCourseSessionBookedNotificationToCoachAsync(string contributionId, string oneToOneCourseTitle,
            string coachUserId, string locationUrl, List<BookedTimeToAvailabilityTime> bookedEventsForCoach, string CustomInvitationBody, bool sendIcalAttachment = true)
        {
            (var coachInvitationCalendarEmail, var coachCommonName, var cohealerTimeZoneId) =
                await GetCoachInvitationEmailInfo(coachUserId);
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x=>x.Id== contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);

            var attachments = _calendarSyncService.CreateICalFile(
                coachCommonName,
                coachInvitationCalendarEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                bookedEventsForCoach,
                CustomInvitationBody);

            var sessionDetails = GetSessionDetails(bookedEventsForCoach, cohealerTimeZoneId);
            var subject = "Confirmed Session(s) for {contributionName}";

            var template = "";
            if (sendIcalAttachment)
            {
                template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                    .ContributionSessionsWasUpdatedNotification);
            }else
                template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                .ContributionSessionsWasUpdatedNotificationForNylas);

            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.ContributionSessionsWasUpdatedNotification), template, contribution, authoruser);
            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);
            
                subject = subject
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress,
                coachInvitationCalendarEmail, subject, finalTemplate,
                attachments, sendIcalAttachment);
        }

        public async Task SendOneToOneCourseSessionBookedNotificationToClientAsync(string contributionId, string oneToOneCourseTitle,
            string clientUserId, string locationUrl, List<BookedTimeToAvailabilityTime> bookedEventsForClient, string CustomInvitationBody, bool sendIcalAttachment = true)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            (var clientEmail, var clientCommonName, var clientTimeZoneId) =
                await GetClientInvitationEmailInfo(clientUserId);

            var attachments = _calendarSyncService.CreateICalFile(
                clientCommonName,
                clientEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                bookedEventsForClient,
                CustomInvitationBody);

            var sessionDetails = GetSessionDetails(bookedEventsForClient, clientTimeZoneId);
            var subject = "Confirmed Session(s) for {contributionName}";
            var template = "";
            if (sendIcalAttachment)
                template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                    .ContributionSessionsWasUpdatedNotification);
            else
                template = await GetTemplateContent(Constants.TemplatesPaths.Contribution
                .ContributionSessionsWasUpdatedNotificationForNylas);

            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.ContributionSessionsWasUpdatedNotification), template, contribution, authoruser);
            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            subject = subject
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress, clientEmail,
               subject, finalTemplate, attachments, sendIcalAttachment);
        }

        public async Task SendOneToOneReschedulingNotificationToCoach(string contributionId, string oneToOneCourseTitle, string coachUserId,
            string reschedulingNotes, string locationUrl, List<BookedTimeToAvailabilityTime> rescheduledEventsForCoach, string CustomInvitationBody, bool sendIcalAttachment = true)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            (var coachInvitationCalendarEmail, var coachCommonName, var cohealerTimeZoneId) =
                await GetCoachInvitationEmailInfo(coachUserId);

            var sessionDetails = GetSessionDetails(rescheduledEventsForCoach, cohealerTimeZoneId);
            var subject = "Confirmed Session(s) for {contributionName}";
            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled);

            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled), template, contribution, authoruser);
            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            finalTemplate = finalTemplate
                .Replace("{reschedulingNotes}",
                    (!string.IsNullOrWhiteSpace(reschedulingNotes))
                        ? $"Reason for reschedule: {reschedulingNotes}"
                        : string.Empty);

                subject = subject
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink)
                .Replace("{reschedulingNotes}",
                    (!string.IsNullOrWhiteSpace(reschedulingNotes))
                        ? $"Reason for reschedule: {reschedulingNotes}"
                        : string.Empty); 

            var attachments = _calendarSyncService.CreateICalFile(
                coachCommonName,
                coachInvitationCalendarEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                rescheduledEventsForCoach,
                CustomInvitationBody);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress,
                coachInvitationCalendarEmail, subject, finalTemplate,
                attachments, sendIcalAttachment);
        }

        public async Task SendOneToOneReschedulingNotificationToClient(string contributionId, string oneToOneCourseTitle, string clientUserId,
            string reschedulingNotes, string locationUrl, List<BookedTimeToAvailabilityTime> rescheduledEventsForClient, string CustomInvitationBody, bool sendIcalAttachment = true)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            (var clientEmail, var clientCommonName, var clientTimeZoneId) =
                await GetClientInvitationEmailInfo(clientUserId);

            var sessionDetails = GetSessionDetails(rescheduledEventsForClient, clientTimeZoneId);
            var subject = "Confirmed Session(s) for {contributionName}";
            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled);
            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled), template, contribution, authoruser);

            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            finalTemplate = finalTemplate
                .Replace("{reschedulingNotes}",
                    (!string.IsNullOrWhiteSpace(reschedulingNotes))
                        ? $"Reason for reschedule: {reschedulingNotes}"
                        : string.Empty);

                 subject = subject
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink)
                .Replace("{reschedulingNotes}",
                    (!string.IsNullOrWhiteSpace(reschedulingNotes))
                        ? $"Reason for reschedule: {reschedulingNotes}"
                        : string.Empty);

            var attachments = _calendarSyncService.CreateICalFile(
                clientCommonName,
                clientEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                rescheduledEventsForClient,
                CustomInvitationBody);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress, clientEmail,
                subject, finalTemplate, attachments, sendIcalAttachment);
        }

        public async Task SendOneToOneCourseSessionEditedNotificationToClientAsync(string contributionId, string oneToOneCourseTitle,
            string clientUserId, string locationUrl, List<BookedTimeToAvailabilityTime> editedEventsForClient, string CustomInvitationBody)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            (var clientEmail, var clientCommonName, var clientTimeZoneId) =
                await GetClientInvitationEmailInfo(clientUserId);

            var sessionDetails = GetSessionDetails(editedEventsForClient, clientTimeZoneId);
            var subject = "Session(s) location for {contributionName} was updated";
            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled);
            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled), template, contribution, authoruser);
            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{reschedulingNotes}", "Session(s) room link was updated")
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

                 subject = subject
                .Replace("{reschedulingNotes}", "Session(s) room link was updated")
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(clientTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            var attachments = _calendarSyncService.CreateICalFile(
                clientCommonName,
                clientEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                editedEventsForClient,
                CustomInvitationBody);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress, clientEmail,
                subject , finalTemplate, attachments);
        }

        public async Task SendOneToOneCourseSessionEditedNotificationToCoachAsync(string contributionId, string oneToOneCourseTitle,
            string coachUserId, string locationUrl, List<BookedTimeToAvailabilityTime> editedEventsForCoach, string CustomInvitationBody)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            var authoruser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            (var coachInvitationCalendarEmail, var coachCommonName, var cohealerTimeZoneId) =
                await GetCoachInvitationEmailInfo(coachUserId);

            var sessionDetails = GetSessionDetails(editedEventsForCoach, cohealerTimeZoneId);
            var subject = "Session(s) location for {contributionName} was updated";
            var template = await GetTemplateContent(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled);
            (string updatedTemplate, string updatedSubject) = GetUpdatedCustomHtmlForEmail(subject, nameof(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled), template, contribution, authoruser);

            template = updatedTemplate;
            subject = updatedSubject;

            var finalTemplate = template
                .Replace("{reschedulingNotes}", "Session(s) room link was updated")
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

                 subject = subject
                .Replace("{reschedulingNotes}", "Session(s) room link was updated")
                .Replace("{sessionsDetails}", sessionDetails)
                .Replace("{timeZoneFriendlyName}", GetFriendlyTimeZoneName(cohealerTimeZoneId))
                .Replace("{contributionName}", oneToOneCourseTitle)
                .Replace("{unsubscribeEmailLink", _unsubscribeEmailLink)
                .Replace("{loginLink}", _loginLink);

            var attachments = _calendarSyncService.CreateICalFile(
                coachCommonName,
                coachInvitationCalendarEmail,
                _sessionNotificationSourceAddress,
                locationUrl,
                editedEventsForCoach, CustomInvitationBody);

            await _emailService.SendWithAttachmentsAsync(_sessionNotificationSourceAddress,
                coachInvitationCalendarEmail, subject,
                finalTemplate, attachments);
        }

        public async Task NotifyNewCoach(Account accountAssociated, User userInserted)
        {
            try
            {
                await SendCoachJoinedNotificationToAllAdmins(
                    accountAssociated.Email,
                    accountAssociated.InvitedBy,
                    userInserted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during notifying admin about new coach");
            }

            // _jobScheduler.ScheduleJob<ISendEmailCoachInstructionGuideJob>(
            //     TimeSpan.FromMinutes(_jobScheduler.Settings.SendCoachInstructionsGuideDelayMinutes),
            //     userInserted.Id);
            //
            // _jobScheduler.ScheduleJob<ISendEmailCoachOneToOneInstructionGuideJob>(
            //     TimeSpan.FromMinutes(_jobScheduler.Settings.SendCoachOneToOneInstructionsGuideDelayMinutes),
            //     userInserted.Id);
        }

        public async Task SendCohrealerPaidTierAccountCancellationNotificationToAdmins(string accountId, User customerUser,
           string planName, string billingFrequency, DateTime? endOfMembershipDate, DateTime? cancellationDate)
        {
            var allAdminsEmail = await GetAllAdminsEmail();

            var customerFullName = $"{customerUser.FirstName}  {customerUser.LastName}";
            var customerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);
            var customerEmail = customerAccount.Email;

            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.PaidtierAccountCancellationEmail);
            var finalEmailTemplate = emailTemplate
                .Replace("{customerName}", customerFullName)
                .Replace("{customerEmail}", customerEmail)
                .Replace("{accountCancellationTime}", cancellationDate.ToString())
                .Replace("{planName}", planName)
                .Replace("{billingFrequency}", billingFrequency?.ToString())
                .Replace("{endOfMembershipDate}", endOfMembershipDate.ToString());

            await _emailService.SendAsync(allAdminsEmail, "Paid tier Account Cancellation.", finalEmailTemplate);
        }

        public async Task SendNotificationBeforeExpirationOfCancelledPlanToAdmins(List<CancelledPlanExpirationEmailModel> modelList)
        {
            var allAdminsEmail = await GetAllAdminsEmail();

            foreach (var emailModel in modelList)
            {
                var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.CancelledPlanExpirationAlertNotification);
                var finalEmailTemplate = emailTemplate
                    .Replace("{customerName}", emailModel.customerName)
                    .Replace("{customerEmail}", emailModel.customerEmail)
                    .Replace("{cancellationDate}", emailModel.cancellationDate.ToString())
                    .Replace("{expireDate}", emailModel.expireDate.ToString());
                await _emailService.SendAsync(allAdminsEmail, "Cancelled account is going to expire soon", finalEmailTemplate);
            }
        }

        public async Task SendNotificationForFailedPayments(DeclinedSubscriptionPurchase declinedSubscriptionPurchase, User user, string billingFrequency, string planName, DateTime? planStartDate)
        {
            var allAdminsEmail = await GetAllAdminsEmail();

            var userAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == user.AccountId);
            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Payment.FailedPaymentNotification);
            var finalEmailTemplate = emailTemplate
                .Replace("{userName}", $"{user.FirstName} {user.LastName}")
                .Replace("{userEmail}", userAccount.Email)
                .Replace("{paymentFailedDate}", declinedSubscriptionPurchase.LastPaymentFailedDate.ToString())
                .Replace("{amountPaid}", declinedSubscriptionPurchase.AmountPaid)
                .Replace("{amountDue}", declinedSubscriptionPurchase.AmountDue)
                .Replace("{amountRemaining}", declinedSubscriptionPurchase.AmountRemaining)
                .Replace("{billingFrequency}", billingFrequency)
                .Replace("{planName}", planName)
                .Replace("{planStartDate}", planStartDate.ToString());
           
            await _emailService.SendAsync(allAdminsEmail, "Failed Payment", finalEmailTemplate);
        }

        public async Task SendNotificationForNewSignupOfPaidtierAccount(string customerName, string customerEmail, string billingFrequency, string planName,
           DateTime? accountCreationTime, DateTime? nextRenewelDate)
        {
            var allAdminsEmail = await GetAllAdminsEmail();
            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Account.NewPaidtierAccountNotification);
            var finalEmailTemplate = emailTemplate
                .Replace("{customerName}", customerName)
                .Replace("{customerEmail}", customerEmail)
                .Replace("{accountCreationTime}", accountCreationTime.ToString())
                .Replace("{billingFrequency}", billingFrequency)
                .Replace("{planName}", planName)
                .Replace("{nextRenewelDate}", nextRenewelDate.ToString());
            await _emailService.SendAsync(allAdminsEmail, "New sign-up for paidtier account", finalEmailTemplate);
        }

        public async Task SendInvoicePaidEmailToCoach(string coachAccountId, string clientEmail, string invoiceNumber, string contributionTitle)
        {
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == coachAccountId);

            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Payment.InvoicePaidEmail);
            var finalEmailTemplate = emailTemplate
                .Replace("{customerEmail}", clientEmail)
                // .Replace("{invoiceNumber}", invoiceNumber)
                .Replace("{contributionTitle}", contributionTitle)
                .Replace("{invoiceDate}", DateTime.UtcNow.ToString());

            await _emailService.SendAsync(coachAccount.Email, $"Invoice has been paid for {contributionTitle}", finalEmailTemplate);
        }

        public async Task SendInvoiceDueEmailToClient(string clientEmail, string clientFirstName, string contributionTitle,string coachFirstName)
        {
            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Payment.InvoiceDueEmail);
            var finalEmailTemplate = emailTemplate
                .Replace("{clientFirstName}", clientFirstName)
                .Replace("{coachFirstName}", coachFirstName)
                .Replace("{contributionTitle}", contributionTitle)
                .Replace("{invoiceDate}", DateTime.UtcNow.ToString());

            await _emailService.SendAsync(clientEmail, $"Invoice Due for {contributionTitle}", finalEmailTemplate);
        }

        public async Task SendInvoiceDueEmailToCoach(string coachAccountId, string clientEmail, string invoiceNumber, string contributionTitle)
        {
            var coachAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == coachAccountId);

            var emailTemplate = await GetTemplateContent(Constants.TemplatesPaths.Payment.InvoiceDueEmailForCoach);
            var finalEmailTemplate = emailTemplate
                //.Replace("{invoiceNumber}", invoiceNumber)
                .Replace("{contributionTitle}", contributionTitle)
                .Replace("{customerEmail}", clientEmail)
                .Replace("{invoiceDate}", DateTime.UtcNow.ToString());
            await _emailService.SendAsync(coachAccount.Email, $"Invoice Due for {contributionTitle}", finalEmailTemplate);
        }

        private async Task<List<string>> GetAllAdminsEmail()
        {
            var allAdmins = await _unitOfWork.GetGenericRepositoryAsync<Account>().
               Get(a => a.Roles.Contains(Roles.Admin) || a.Roles.Contains(Roles.SuperAdmin));
            return allAdmins.Select(a => a.Email).ToList();
        }

        private bool IsEmailNotificationEnabled(Account userAccount)
        {
            return userAccount.IsEmailConfirmed && userAccount.IsEmailNotificationsEnabled;
        }

        private string GetPerTotalAmountPeriodSuffix(PaymentOptions paymentOption)
        {
            return paymentOption switch
            {
                PaymentOptions.DailyMembership => "per Day",
                PaymentOptions.WeeklyMembership => "per Week",
                PaymentOptions.MonthlyMembership => "per Month",
                PaymentOptions.YearlyMembership => "per Year",
                _ => string.Empty
            };
        }

        private async Task<string> GetInvitedByPlaceholderValue(string invitedBy)
        {
            var affiliateAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == invitedBy);
            var affiliateUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == affiliateAccount.Id);

            return $"Referred by {affiliateUser.FirstName} {affiliateUser.LastName}({affiliateAccount.Email})";
        }

        private async Task<List<SessionInfoForReminderViewModel>> GetSessionReminderInfos(DateTime dateTimeStart, DateTime dateTimeEnd)
        {
            var contributionsUncompleted = await GetUncompletedContributions(dateTimeStart, dateTimeEnd);

            var contributionsUncompletedVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(contributionsUncompleted).ToList();

            var sessionInfosForReminders = new List<SessionInfoForReminderViewModel>();
            foreach (var contribution in contributionsUncompletedVms)
            {
                await FillPodsForSessionContribution(contribution);
                sessionInfosForReminders.AddRange(contribution.GetTomorrowSessions(dateTimeStart, dateTimeEnd));
            };

            return sessionInfosForReminders;
        }

        private async Task<List<SessionInfoForReminderViewModel>> GetPartnerCoachSessionInfos(DateTime dateTimeStart, DateTime dateTimeEnd)
        {
            var contributionsUncompleted = await GetUncompletedContributions(dateTimeStart, dateTimeEnd);

            var sessionInfosForReminders = new List<SessionInfoForReminderViewModel>();

            foreach (var contribution in contributionsUncompleted)
            {
                var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
                await FillPodsForSessionContribution(contributionVm);
                foreach (var partner in contribution.Partners)
                {
                    var sessions = contributionVm.GetTomorrowSessions(dateTimeStart, dateTimeEnd);
                    sessions.ForEach(x =>
                    {
                        x.AuthorUserId = partner.UserId;
                    });
                    sessionInfosForReminders.AddRange(sessions);
                }
            }

            return sessionInfosForReminders;
        }

        private async Task<IEnumerable<ContributionBase>> GetUncompletedContributions(DateTime tomorrowStartMomentUtc, DateTime dayAfterTomorrowStartMomentUtc)
        {
            return await _contributionRootService.Get(c =>
                    c.Status == ContributionStatuses.Approved &&
                    c.CohealerBusyTimeRanges.Any(tr => tr.StartTime >= tomorrowStartMomentUtc && tr.StartTime <= dayAfterTomorrowStartMomentUtc));
        }

        private async Task FillPodsForSessionContribution(ContributionBaseViewModel contributionVm)
        {
            if (contributionVm is SessionBasedContributionViewModel vm)
            {
                var podIds = vm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                vm.Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
            }
        }

        private string GetFriendlyTimeZoneName(string timeZone)
        {
            bool foundInDictionary = DateTimeHelper.TimeZoneFriendlyNames.TryGetValue(timeZone, out string timeZoneName);
            return foundInDictionary ? DateTimeHelper.TimeZoneFriendlyNames[timeZone] : timeZone;
    }

        public async Task SendTestEmailNotification(string accountId, CustomTemplate customTemplate)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == customTemplate.ContributionId);
            var AuthorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var AuthorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);
            string emailHtmlTemplate = customTemplate.EmailText;
            string subject = customTemplate.EmailSubject;

            if (customTemplate.IsCustomBrandingColorsEnabled)
            {
                var customColors = GetBrandingColorsForEmails(contribution, AuthorUser);
                emailHtmlTemplate = emailHtmlTemplate
                .Replace("#0b6481", customColors.AccentColorCode)
                .Replace("#d1b989", customColors.PrimaryColorCode);
            }
            emailHtmlTemplate = emailHtmlTemplate
                .Replace("{contributionName}", contribution.Title)
                .Replace("{cohealerFirstName}", AuthorUser.FirstName);

            subject = subject
                .Replace("{contributionName}", contribution.Title)
                .Replace("{cohealerFirstName}", AuthorUser.FirstName);

            await _emailService.SendAsync(AuthorAccount.Email, subject, emailHtmlTemplate);
        }
    
    }
}
