using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Account;
using Cohere.Entity.Entities;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IAccountManager
    {
        Task<bool> IsEmailAvailableForRegistration(string email);

        Task<OperationResult> ChangePassword(ChangePasswordViewModel changePasswordViewModel, string requesterAccountId);

        Task<OperationResult> ConfirmAccountEmailAsync(TokenVerificationViewModel confirmationModel);

        Task<OperationResult> RestorePasswordByLinkRequestAsync(string email);

        Task<OperationResult> RestorePasswordByAnswersRequestAsync(string email);

        Task<OperationResult> VerifyPasswordRestorationLinkAsync(TokenVerificationViewModel verificationModel);

        Task<OperationResult> VerifySecurityAnswersAsync(RestoreBySecurityAnswersViewModel securityAnswersModel);

        Task<OperationResult> RestorePasswordAsync(RestorePasswordViewModel restoreModel);

        Task<OperationResult> RequestEmailConfirmationAsync(string accountId);

        Task<bool> IsAdminOrSuperAdmin(string userId);

        Task<bool> IsVideoTestFirstTime(string accountId);

        Task HidePaidTierOptionBanner(string accountId);

        Task<OperationResult> UpdateCoachLoginInfo(string accountId, CoachLoginInfo coachLoginInfo);
    }
}
