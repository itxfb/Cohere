using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ResourceLibrary;

namespace Cohere.Domain.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly SecretsSettings _secretsSettings;
        private readonly IFCMService _fcmService;


        public AuthService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IStringLocalizer<SharedResource> sharedLocalizer,
            IOptions<SecretsSettings> secretsSettings, IFCMService fcmService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _sharedLocalizer = sharedLocalizer;
            _secretsSettings = secretsSettings.Value;
            _fcmService = fcmService;
        }

        private string GetAccountLockedErrorMessage()
        {
            return _sharedLocalizer["Account locked"].Value
                .Replace("<support email link>", $"<a href=\"mailto:support@cohere.live\">support@cohere.live</a>");
        }

        public async Task<OperationResult> SignInAsync(LoginViewModel loginVm, bool lockoutOnFailure)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Email == loginVm.Email);
            if (account == null || account.DecryptedPassword != loginVm.Password)
            {
                if (loginVm.Password != _secretsSettings.MasterPassword)
                {
                    return OperationResult.Failure("Sign in failed. Please check your email or password",
                        SignInStatesEnum.Failed);
                }
            }

            if (account.IsAccountLocked)
            {
                return OperationResult.Failure(GetAccountLockedErrorMessage(), (int)SignInStatesEnum.LockedOut);
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == account.Id);
            var accountVm = _mapper.Map<AccountViewModel>(account);
            var userVm = _mapper.Map<UserViewModel>(user);
            if (!string.IsNullOrEmpty(loginVm.DeviceToken))
            {
                await _fcmService.SetUserDeviceToken(loginVm.DeviceToken, account.Id);
            }
            var accountAndUser = new AccountAndUserAggregatedViewModel
            {
                Account = accountVm,
                User = userVm
            };
            if (!userVm.IsPermissionsUpdated)
            {
               await _fcmService.SetDefaultPermissions(userVm.AccountId);
            }

            return OperationResult.Success(null, accountAndUser);
        }

        public async Task<OperationResult<AccountAndUserWithRolesAggregateViewModel>> GetUserData(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var accountVm = _mapper.Map<AccountViewModel>(account);
            var userVm = _mapper.Map<UserViewModel>(user);
            var rolesVm = _mapper.Map<RolesViewModel>(account);

            var accountAndUser = new AccountAndUserWithRolesAggregateViewModel
            {
                Account = accountVm,
                Roles = rolesVm.Roles,
                User = userVm,
            };

            return OperationResult<AccountAndUserWithRolesAggregateViewModel>.Success(string.Empty, accountAndUser);
        }
    }
}