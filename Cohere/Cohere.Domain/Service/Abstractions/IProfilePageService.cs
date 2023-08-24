using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.User;
using Cohere.Entity.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IProfilePageService
    {
        Task<OperationResult> Insert(ProfilePageViewModel viewModel, string creatorAccountId);

        Task<OperationResult> Update(string Id, ProfilePageViewModel viewModel, string requesterAccountId);
        Task<ProfilePage> GetProfilePage(string AccountId);
        Task<OperationResult> GetProfileLinkNameByContributionId(string contributionId);
        Task<OperationResult> GetProfilePageByName(string uniquename);
        Task<OperationResult> InsertOrUpdateProfilePage(ProfilePageViewModel viewModel, string accountId);

        Task<OperationResult> AddFollowerToProfile(string userAccountId, string profileAccountId);

        Task<OperationResult> RemoveFollowerFromProfile(string userAccountId, string profileAccountId);

        Task<List<ProfileFollowers>> GetAllFollowers(string accountId);

        Task<OperationResult> AddCustomLink(List<CustomLinksViewModel> viewModel, string profileAccountId);

        Task<OperationResult> UpdateCustomLinkByUniqueName(CustomLinksViewModel viewModel, string profileAccountId, string uniqueName);

        Task<CustomLinksViewModel> GetAllCustomLinks(string profileAccountId);

        Task<OperationResult> SwitchCustomLinkVisibilty(bool IsVisible, string profileAccountId, string uniqueName);
        Task<IEnumerable<ProfileFollowersDTO>> GetProfileFollowersDetailsAsync(string profileAccountId);

        Task<OperationResult> UpdateProfilepageContribution(string profileUserId, ContributionBaseViewModel contributionModel);

    }
}
