using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Account;
using Cohere.Entity.UnitOfWork;

using Microsoft.Extensions.Localization;
using ResourceLibrary;

namespace Cohere.Domain.Service
{
    public class AccountManager : IAccountManager
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly INotificationService _notificationService;
        private readonly IUserService<UserViewModel, User> _userService;
        private readonly IContributionService _contributionService;
        private readonly IContributionStatusService _contributionStatusService;
        private readonly IMapper _mapper;
        private readonly int _passwordRestorationTokenLifetimeDays;
        private readonly int _verificationTokenLifetimeDays;

        public const string PasswordRestorationTokenLifetimeDays = nameof(PasswordRestorationTokenLifetimeDays);
        public const string VerificationTokenLifetimeDays = nameof(VerificationTokenLifetimeDays);

        public AccountManager(
            IUnitOfWork unitOfWork,
            IStringLocalizer<SharedResource> sharedLocalizer,
            INotificationService notificationService,
            IUserService<UserViewModel, User> userService,
            IContributionService contributionService,
            IContributionStatusService contributionStatusService,
            IMapper mapper,
            Func<string, int> tokenLifetimeSettingsResolver)
        {
            _unitOfWork = unitOfWork;
            _sharedLocalizer = sharedLocalizer;
            _notificationService = notificationService;
            _userService = userService;
            _contributionService = contributionService;
            _contributionStatusService = contributionStatusService;
            _mapper = mapper;
            _passwordRestorationTokenLifetimeDays = tokenLifetimeSettingsResolver.Invoke(PasswordRestorationTokenLifetimeDays);
            _verificationTokenLifetimeDays = tokenLifetimeSettingsResolver.Invoke(VerificationTokenLifetimeDays);
        }

        public async Task<bool> IsEmailAvailableForRegistration(string email)
        {
            var existedUsers = await _unitOfWork.GetRepositoryAsync<Account>().Get(a => a.Email == email.ToLower());
            return !existedUsers.Any();
        }

        public async Task<OperationResult> ChangePassword(ChangePasswordViewModel changePasswordViewModel, string requesterAccountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == changePasswordViewModel.Email);
            if (account == null)
            {
                return OperationResult.Failure($"Unable to find account with email: {changePasswordViewModel.Email}");
            }

            if (account.Id != requesterAccountId)
            {
                var requesterAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requesterAccountId);
                var isAdmin = requesterAccount.Roles.Contains(Roles.Admin) || requesterAccount.Roles.Contains(Roles.SuperAdmin);
                if (!isAdmin)
                {
                    return OperationResult.Failure($"Forbidden to change password for other user");
                }
            }

            if (account.DecryptedPassword != changePasswordViewModel.CurrentPassword)
            {
                return OperationResult.Failure(_sharedLocalizer["Wrong old password"]);
            }

            if (account.DecryptedPassword == changePasswordViewModel.NewPassword)
            {
                return OperationResult.Failure(_sharedLocalizer["Same as old password"]);
            }

            account.DecryptedPassword = changePasswordViewModel.NewPassword;
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult.Success(_sharedLocalizer["Password changed"].Value
                .Replace("<email>", account.Email));
        }

        public async Task<OperationResult> ConfirmAccountEmailAsync(TokenVerificationViewModel confirmationModel)
        {
            var accountForEmailConfirm = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(a => a.Email == confirmationModel.Email);

            if (accountForEmailConfirm != null && accountForEmailConfirm.IsEmailConfirmed)
            {
                return OperationResult.Success("You email have been already confirmed");
            }

            var account = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(a => a.Email == confirmationModel.Email && a.VerificationToken == confirmationModel.Token);
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Incorrect email or verification token"].Value);
            }

            if (account.IsAccountLocked)
            {
                return OperationResult.Failure(GetAccountLockedErrorMessage());
            }

            account.VerificationToken = null;
            account.IsEmailConfirmed = true;

            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            var user = await _userService.GetByAccountIdAsync(account.Id);

            if (user.TransfersEnabled)
            {
                await _contributionStatusService.ExposeContributionsToReviewAsync(user.Id);
            }

            return OperationResult.Success(_sharedLocalizer["Email confirmed"]);
        }

        private string GetAccountLockedErrorMessage()
        {
            return _sharedLocalizer["Account locked"].Value
                                .Replace("<support email link>", $"<a href=\"mailto:support@cohere.live\">support@cohere.live</a>");
        }

        public async Task<OperationResult> RestorePasswordByLinkRequestAsync(string email)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == email.ToLower());
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Account with email does not exist"].Value);
            }

            if (account.IsAccountLocked)
            {
                return OperationResult.Failure(GetAccountLockedErrorMessage());
            }

            account.PasswordRestorationToken = Guid.NewGuid().ToString();
            account.PasswordRestorationTokenExpiration = DateTime.Now.AddDays(_passwordRestorationTokenLifetimeDays);
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            await _notificationService.SendPasswordResetLink(account.Id, account.Email, account.PasswordRestorationToken);

            var successResponse = _sharedLocalizer["Password restoration link sent"].Value;
            return OperationResult.Success(successResponse.Replace("<email>", account.Email));
        }

        public async Task<OperationResult> RestorePasswordByAnswersRequestAsync(string email)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == email.ToLower());
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Account with email does not exist"].Value);
            }

            if (account.IsAccountLocked)
            {
                return OperationResult.Failure(GetAccountLockedErrorMessage());
            }

            var questions = await _unitOfWork.GetRepositoryAsync<SecurityQuestion>()
                .Get(x => account.SecurityAnswers.Keys.Contains(x.Id));

            return OperationResult.Success(null, _mapper.Map<IEnumerable<SecurityQuestionViewModel>>(questions));
        }

        public async Task<OperationResult> VerifyPasswordRestorationLinkAsync(TokenVerificationViewModel verificationModel)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(a => a.Email == verificationModel.Email && a.PasswordRestorationToken == verificationModel.Token);
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Incorrect email or restoration token"].Value);
            }

            if (account.PasswordRestorationTokenExpiration < DateTime.Now)
            {
                return OperationResult.Failure(_sharedLocalizer["Expired Password restoration token"].Value);
            }

            account.PasswordRestorationToken = Guid.NewGuid().ToString();
            account.PasswordRestorationTokenExpiration = DateTime.Now.AddDays(_passwordRestorationTokenLifetimeDays);
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult.Success(_sharedLocalizer["Password restoration token verified"], account.PasswordRestorationToken);
        }

        public async Task<OperationResult> VerifySecurityAnswersAsync(RestoreBySecurityAnswersViewModel securityAnswersModel)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == securityAnswersModel.Email);
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Account with email does not exist"].Value);
            }

            if (account.IsAccountLocked)
            {
                return OperationResult.Failure(GetAccountLockedErrorMessage());
            }

            if (!account.SecurityAnswers.Any())
            {
                return OperationResult.Failure(_sharedLocalizer["Account does not have security answers"]);
            }

            if (account.SecurityAnswers.Any(x =>
                !securityAnswersModel.SecurityAnswers.TryGetValue(x.Key, out var answer) || x.Value != answer))
            {
                return OperationResult.Failure(_sharedLocalizer["Incorrect security answers"]);
            }

            account.PasswordRestorationToken = Guid.NewGuid().ToString();
            account.PasswordRestorationTokenExpiration = DateTime.Now.AddDays(_passwordRestorationTokenLifetimeDays);
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult.Success(_sharedLocalizer["Correct security answers"], account.PasswordRestorationToken);
        }

        public async Task<OperationResult> RestorePasswordAsync(RestorePasswordViewModel restoreModel)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == restoreModel.Email && a.PasswordRestorationToken == restoreModel.Token);
            if (account == null)
            {
                return OperationResult.Failure(_sharedLocalizer["Incorrect email or restoration token"].Value);
            }

            if (account.PasswordRestorationTokenExpiration < DateTime.Now)
            {
                return OperationResult.Failure(_sharedLocalizer["Expired password restoration token"].Value);
            }

            // confirm email since the user clicked on the reset password link from their email
            if(!account.IsEmailConfirmed)
			{
                account.IsEmailConfirmed = true;
            }

            account.PasswordRestorationToken = null;
            account.DecryptedPassword = restoreModel.NewPassword;
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult.Success(_sharedLocalizer["Password changed"].Value.Replace("<email>", account.Email));
        }

        public async Task<OperationResult> RequestEmailConfirmationAsync(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);
            if (account == null)
            {
                return OperationResult.Failure("Account was not found");
            }

            // do not sent this notification if accout has signup type other than none
            if (account.SignupType == SignupTypes.NONE)
            {

                if (account.IsEmailConfirmed)
                {
                    return OperationResult.Failure("Unable to request email confirmation. Account email has been already confirmed");
                }

                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
                if (user == null)
                {
                    return OperationResult.Failure("User was not found");
                }

                account.VerificationTokenExpiration = DateTime.Now.AddDays(_verificationTokenLifetimeDays);
                account.VerificationToken = Guid.NewGuid().ToString();

                await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);
                await _notificationService.SendEmailConfirmationLink(account.Email, account.VerificationToken, user.IsCohealer);
            }

            return OperationResult.Success(null);
        }

        public async Task<bool> IsAdminOrSuperAdmin(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);
            return account.Roles.Contains(Roles.Admin) || account.Roles.Contains(Roles.SuperAdmin);
        }

        public async Task<bool> IsVideoTestFirstTime(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);

            if (account.IsVideoTestFirstTime)
            {
                account.IsVideoTestFirstTime = false;
                await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);
                return true;
            }

            return false;
        }

        public async Task HidePaidTierOptionBanner(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);
            account.PaidTierOptionBannerHidden = true;

            await _unitOfWork.GetRepositoryAsync<Account>().Update(accountId, account);
        }

        public async Task<OperationResult> UpdateCoachLoginInfo(string accountId, CoachLoginInfo coachLoginInfo)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);

            if (account == null)
            {
                return OperationResult.Failure(
                    $"Unable to find account with Id:{accountId}");
            }

            account.CoachLoginInfo = coachLoginInfo;
            var accountResult = await _unitOfWork.GetRepositoryAsync<Account>().Update(accountId, account);
            var accountVmResult = _mapper.Map<AccountViewModel>(accountResult);

            return OperationResult.Success(null, accountVmResult);
        }
    }
}
