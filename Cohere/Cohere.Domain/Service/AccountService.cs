using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Generic;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.User;
using Cohere.Entity.UnitOfWork;

using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Service
{
    public class AccountService<TViewModel, TEntity> : GenericServiceAsync<TViewModel, TEntity>,
        IAccountService<TViewModel, TEntity>
        where TViewModel : AccountViewModel
        where TEntity : Account
    {
        private readonly ILogger<AccountService<TViewModel, TEntity>> _logger;
        private readonly IAffiliateCommissionService _affiliateCommissionService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly int _verificationTokenLifetimeDays;

        public const string VerificationTokenLifetimeDays = nameof(VerificationTokenLifetimeDays);

        public AccountService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<AccountService<TViewModel, TEntity>> logger,
            IAffiliateCommissionService affiliateCommissionService,
            IActiveCampaignService activeCampaignService,
            Func<string, int> verificationTokenLifetimeDaysResolver)
            : base(unitOfWork, mapper)
        {
            _logger = logger;
            _affiliateCommissionService = affiliateCommissionService;
            _activeCampaignService = activeCampaignService;
            _verificationTokenLifetimeDays =
                verificationTokenLifetimeDaysResolver.Invoke(VerificationTokenLifetimeDays);
        }

        public override async Task<OperationResult> Insert(TViewModel accountVm)
        {
            var existedUsers = await _unitOfWork.GetRepositoryAsync<Account>().Get(a => a.Email == accountVm.Email);

            if (existedUsers.Any())
            {
                return OperationResult.Failure($"Account with email exists: {accountVm.Email} ");
            }

            var entity = Mapper.Map<Account>(accountVm);

            entity.OnboardingStatus = OnboardingStatuses.Registered;
            entity.IsAccountLocked = false;
            entity.IsPushNotificationsEnabled = true;
            entity.IsEmailNotificationsEnabled = true;
            entity.IsEmailConfirmed = false;
            entity.IsPhoneConfirmed = false;
            entity.DecryptedPassword = accountVm.Password;
            entity.Roles = new List<Roles> {Roles.Client};
            entity.VerificationTokenExpiration = DateTime.Now.AddDays(_verificationTokenLifetimeDays);
            entity.VerificationToken = Guid.NewGuid().ToString();
            entity.PasswordRestorationTokenExpiration = DateTime.Now;
            entity.IsVideoTestFirstTime = true;
            entity.CoachLoginInfo = new CoachLoginInfo();

            await SetRevenueLimits(entity);
            SetDefaultAffiliateProgramConfiguration(entity);

            var insertedAccount = await _unitOfWork.GetRepositoryAsync<Account>().Insert(entity);

            var insertedVm = Mapper.Map<TViewModel>(insertedAccount);

            return OperationResult.Success("Account inserted", insertedVm);

            async Task SetRevenueLimits(Account account)
            {
                try
                {
                    if (account.InvitedBy != null)
                    {
                        var revenueLimits =
                            await _affiliateCommissionService.GetAffiliateRevenueLimitsByInviteCode(account.InvitedBy);

                        if (revenueLimits.Succeeded)
                        {
                            account.AffiliateRevenueLimits = revenueLimits.Payload;
                        }
                        else
                        {
                            _logger.LogError(
                                $"Error during getting revenue limits for invite code {account.InvitedBy}. Operation result message: {revenueLimits.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during setting revenue limits");
                }
            }

            void SetDefaultAffiliateProgramConfiguration(Account account)
            {
                try
                {
                    var revenueLimits = _affiliateCommissionService.GetDefaultAffiliateProgramConfiguration();

                    if (revenueLimits.Succeeded)
                    {
                        account.AffiliateProgramConfiguration = revenueLimits.Payload;
                    }
                    else
                    {
                        _logger.LogError(
                            $"Error during getting revenue limits for invite code {account.InvitedBy}. Operation result message: {revenueLimits.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during setting revenue limits");
                }
            }
        }

        public override async Task<OperationResult> Update(TViewModel accountVm)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountVm.Id);
            if (account == null)
            {
                return OperationResult.Failure($"Unable to find account with Id:{accountVm.Id}");
            }

            var updatedAccount = Mapper.Map<Account>(accountVm);
            updatedAccount.DecryptedPassword = account.DecryptedPassword;
            updatedAccount.Roles = account.Roles;
            updatedAccount.OnboardingStatus = account.OnboardingStatus;
            updatedAccount.Email = account.Email;
            updatedAccount.IsEmailConfirmed = account.IsEmailConfirmed;
            updatedAccount.NumLogonAttempts = account.NumLogonAttempts;
            updatedAccount.ZoomRefreshToken = account.ZoomRefreshToken;
            updatedAccount.ZoomUserId = account.ZoomUserId;
            updatedAccount.CoachLoginInfo = account.CoachLoginInfo;

            var accountResult = await _unitOfWork.GetRepositoryAsync<Account>().Update(accountVm.Id, updatedAccount);
            var accountVmResult = Mapper.Map<AccountViewModel>(accountResult);

            return OperationResult.Success("Account updated", accountVmResult);
        }

        public async Task<OperationResult<TViewModel>> GetByEmail(string email)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().Get(a => a.Email == email);
            var resultModel = Mapper.Map<TViewModel>(account);
            return new OperationResult<TViewModel>(resultModel);
        }

        public async Task<OperationResult<AccountPreferencesViewModel>> SetUserPreferences(string accountId,
            AccountPreferencesViewModel model)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);

            if (account == null)
            {
                return OperationResult<AccountPreferencesViewModel>.Failure(
                    $"Unable to find account with Id:{accountId}");
            }

            account.AccountPreferences = Mapper.Map<AccountPreferences>(model);

            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult<AccountPreferencesViewModel>.Success(model);
        }
    }
}