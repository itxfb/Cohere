using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Entity.Entities;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure.Generic;
using System.Collections.Generic;
using Cohere.Entity.UnitOfWork;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IUserService<TViewModel, TEntity> : IServiceAsync<TViewModel, TEntity>
        where TViewModel : UserViewModel
        where TEntity : User
    {
        Task<UserViewModel> GetByAccountIdAsync(string accountId);

        Task<User> GetUserAsync(string accountId);
        Task<User> GetUserWithUserId(string userId);

        Task<Account> GetAccountAsync(string accountId);

        Task<User> GetCohealerIconByContributionId(string contributionId);

        Task<OperationResult> SaveUserSocialLastReadTime(string userId, string contributionId);

        Task<OperationResult> GetCountsOfNonReadedSocialPostsForContributions(string userId, IEnumerable<string> contributionIds);
        Task<OperationResult> GetTotalCountsOfNonReadedSocialPostsForContributions(string userId, IEnumerable<string> contributionIds);
        Task<OperationResult<UserActivity>> LogUserActivity(string userId);
        Task<OperationResult<Entity.Entities.Messages>> GetPopupMessage();
        Task<OperationResult<UserDetailModel>> GetUserDetails(string userId);
        Task<OperationResult> GetClientAndContributionDetailForZapier(string accountId);
        Task<OperationResult> AddProfileLinkName(string accountId, string profileLinkName);
        Task<OperationResult> CheckProfileLinkName(string profileLinkName);
        Task<OperationResult> EnableCustomEmailNotification(string accountId, bool _enableCustomEmailNotification);
        Task<OperationResult> UpdateUserProfileColors(string userId, Dictionary<string, string> BrandingColors, string customLogo = "");
        bool IsBrandingLogoChanged(User dbUser, UserViewModel modelUser);
        bool IsBrandingColorChnaged(User dbUser, UserViewModel modelUser);
        Task<OperationResult> GetAndSaveUserProgressbarData(UserViewModel user);
        int GetProgressbarPercentage(Dictionary<string, bool> progressbarData);
        Task<OperationResult> UpdateUserFromDynamicObjectAsync(UserDTO userObject);
    }

    public interface IRoleSwitchingService
    {
        Task<OperationResult> SwitchFromClientToCoach(string accountId, SwitchFromClientToCoachViewModel model);

        Task<OperationResult<string>> SwitchFromCoachToClient(string accountId);
    }
}
