using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Community.UserInfo;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class ProfilePageService : IProfilePageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProfilePageService(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OperationResult> Insert(ProfilePageViewModel viewModel, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (user.Id != viewModel.UserId)
            {
                return OperationResult.Failure("It is not allowed to add profile contribution for other author");
            }
            var existedUsers = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(a => a.UserId == user.Id);

            if (existedUsers != null)
            {
                return OperationResult.Failure($"Data with UserId exists: {viewModel.UserId} ");
            }
            var entity = _mapper.Map<ProfilePage>(viewModel);

            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Insert(entity);
            return OperationResult.Success("Object inserted", viewModel);
        }


        public async Task<OperationResult> Update(string Id, ProfilePageViewModel viewModel, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (user.Id != viewModel.UserId)
            {
                return OperationResult.Failure("It is not allowed to update profile contribution for other author");
            }
            var entity = _mapper.Map<ProfilePage>(viewModel);

            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(Id, entity);

            return OperationResult.Success("Object inserted", viewModel);
        }


        public async Task<ProfilePage> GetProfilePage(string AccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == AccountId);
            var result = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(x => x.UserId == user.Id);
            return result;
        }
        public async Task<OperationResult> GetProfileLinkNameByContributionId(string contributionId)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            if (contribution == null)
            {
                return OperationResult.Failure($"Contribution does not exists against Id: {contributionId}");
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
            if (user == null)
            {
                return OperationResult.Failure($"User does not exists against Id: {contribution.UserId}");
            }
            return OperationResult.Success(string.Empty, user.ProfileLinkName);
        }
        public async Task<OperationResult> GetProfilePageByName(string uniquename)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.ProfileLinkName.ToLower() == uniquename.ToLower());
            if (user != null)
            {
                var result = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(x => x.UserId == user.Id);
                var entity = _mapper.Map<UserViewModel>(user);
                if (result != null)
                {
                    var profileviewmodel = _mapper.Map<ProfilePageViewModel>(result);
                    entity.ProfilePageViewModel = profileviewmodel;
                }
                return OperationResult.Success(String.Empty, entity);
            }
            return OperationResult.Failure("User with this profile name not exists");
        }

        public async Task<OperationResult> InsertOrUpdateProfilePage(ProfilePageViewModel viewModel, string accountId)
        {

            var userProfilePage = await GetProfilePage(accountId);

            OperationResult result;

            if (userProfilePage == null)
            {
                result = await Insert(viewModel, accountId);
            }
            else
            {
                result = await Update(userProfilePage.Id, viewModel, accountId);
            }

            return result;

        }

        public async Task<List<ProfileFollowers>> GetAllFollowers(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == accountId);

            //var allPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributorId == user.Id);

            //var allPurchasesVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(allPurchases).ToList();

            //var participantsWithAccess = allPurchasesVms
            //    .Where(p => p.HasAccessToContribution)
            //    .ToList();

            //var participantsIds = participantsWithAccess
            //    .Select(p => p.ClientId)
            //    .ToList();


            //var participants = await _unitOfWork.GetRepositoryAsync<User>().Get(u => participantsIds.Contains(u.Id));

            var userProfilePage = await GetProfilePage(accountId);

            if (userProfilePage == null)
            {
                return null;
            }

            var followers = userProfilePage.Followers;

            return followers.Distinct().ToList();

        }


        public async Task<OperationResult> AddFollowerToProfile(string userAccountId, string profileAccountId)
        {

            if (userAccountId == profileAccountId)
            {
                return OperationResult.Failure("user cannot unfollow or follow himself !");
            }


            var follower = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == userAccountId);

            if (follower == null)
            {
                return OperationResult.Failure("User not found with Id :" + userAccountId);
            }

            var profile = await GetProfilePage(profileAccountId);


            if (profile == null)
            {
                return OperationResult.Failure("Profile not found with Id :" + profileAccountId);

            }

            var createFollower = new ProfileFollowers();
            createFollower.CreateTime = DateTime.Now;
            createFollower.FollowerId = follower.Id;


            if (profile.Followers.Count() == 0)
            {
                profile.Followers.Add(createFollower);
            }
            else if (!profile.Followers.TrueForAll(a => a.FollowerId.Contains(follower.Id)))
            {

                profile.Followers.Add(createFollower);
            }
            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);

            return OperationResult.Success();

        }


        public async Task<OperationResult> RemoveFollowerFromProfile(string userAccountId, string profileAccountId)
        {

            if (userAccountId == profileAccountId)
            {
                return OperationResult.Failure("user cannot unfollow or follow himself !");
            }

            var profile = await GetProfilePage(profileAccountId);
            if (profile == null)
            {
                return OperationResult.Failure("Profile not found with Id :" + profileAccountId);

            }

            var followerToRemove = new ProfileFollowers()
            {
                FollowerId = userAccountId
            };

            profile.Followers.Remove(followerToRemove);


            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);


            return OperationResult.Success();
        }

        public async Task<OperationResult> AddCustomLink(List<CustomLinksViewModel> viewModel, string profileAccountId)
        {

            var profile = await GetProfilePage(profileAccountId);

            if (profile == null)
            {
                return OperationResult.Failure("Profile not found with Id :" + profileAccountId);
            }

            var customLinks = _mapper.Map<List<CustomLinks>>(viewModel);
            //var exsistingLinks = await _unitOfWork.GetRepositoryAsync<ProfilePage>().Get(a => a.CustomLinks.Select(a=>a.UniqueName).Equals(customLinks.Select(c=>c.UniqueName)) && a.UserId!= user.Id).First();
            var exsistingLinks = profile.CustomLinks.Where(a => a.UniqueName.Equals(viewModel.Select(a => a.UniqueName))).FirstOrDefault();

            if (exsistingLinks != null)
            {
                return OperationResult.Failure("Custom Link with Unique Name " + exsistingLinks.UniqueName + " already exsists");
            }

            profile.CustomLinks.AddRange(customLinks);

            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);

            return OperationResult.Success();
        }


        public async Task<CustomLinksViewModel> GetAllCustomLinks(string profileAccountId)
        {

            var profile = await GetProfilePage(profileAccountId);

            if (profile == null)
            {
                return null;
            }

            return _mapper.Map<CustomLinksViewModel>(profile.CustomLinks);

        }


        public async Task<OperationResult> UpdateCustomLinkByUniqueName(CustomLinksViewModel viewModel, string profileAccountId, string uniqueName)
        {

            var profile = await GetProfilePage(profileAccountId);

            if (profile == null)
            {
                return OperationResult.Failure("Profile not found with Id :" + profileAccountId);

            }
            var exsistingLinks = profile.CustomLinks.Where(a => a.UniqueName.Equals(viewModel.UniqueName) && a.UniqueName != uniqueName).FirstOrDefault();

            if (exsistingLinks != null)
            {
                return OperationResult.Failure("Custom link with :" + viewModel.UniqueName + "already exsists");
            }

            var profilelink = profile.CustomLinks.Where(a => a.UniqueName == uniqueName).First();

            if (profilelink == null)
            {
                return OperationResult.Failure("Profile link not found with name :" + uniqueName);
            }

            profilelink = _mapper.Map<CustomLinks>(viewModel);

            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);

            return OperationResult.Success();

        }


        public async Task<OperationResult> SwitchCustomLinkVisibilty(bool IsVisible, string profileAccountId, string uniqueName)
        {

            var profile = await GetProfilePage(profileAccountId);

            if (profile == null)
            {
                return OperationResult.Failure("Profile not found with Id :" + profileAccountId);

            }

            var profilelink = profile.CustomLinks.Where(a => a.UniqueName == uniqueName).First();

            if (profilelink == null)
            {
                return OperationResult.Failure("Profile link not found with name :" + uniqueName);
            }

            profilelink.IsVisible = IsVisible;

            await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);

            return OperationResult.Success();

        }

        public async Task<IEnumerable<ProfileFollowersDTO>> GetProfileFollowersDetailsAsync(string profileAccountId)
        {
            var profile = await GetProfilePage(profileAccountId);

            if (profile == null)
            {
                return null;

            }

            var followers = profile.Followers;

            if (followers.Count() == 0)
            {
                return null;
            }

            var clientsData = new List<ProfileFollowersDTO>();

            foreach (var x in followers)
            {

                var clientDetails = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.Id == x.FollowerId);
                var clientAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == clientDetails.AccountId);


                var obj = new ProfileFollowersDTO();
                obj.Email = clientAccount.Email;
                obj.FirstName = clientDetails.FirstName;
                obj.LastName = clientDetails.LastName;
                obj.DateJoined = x.CreateTime;

                clientsData.Add(obj);


            }


            return clientsData.AsEnumerable();


        }
        public async Task<OperationResult> UpdateProfilepageContribution(string profileUserId, ContributionBaseViewModel contributionModel)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == profileUserId);
            if(user == null)
            {
                return OperationResult.Failure("User Not Exists");
            }

            var profile = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(x => x.UserId == profileUserId);


            if (profile != null)
            {
                var contributionDTOs = profile.Contributions.Where(x => x.Id == contributionModel.Id).ToList();
                bool needToUpdate = false;
                foreach (var contributiondto in contributionDTOs)
                {
                    contributiondto.Title = contributionModel.Title;
                    contributiondto.Description = contributionModel.Purpose;
                    contributiondto.ImageUrl = contributionModel?.PreviewContentUrls?.FirstOrDefault();
                    needToUpdate = true;
                }
                if (needToUpdate)
                    await _unitOfWork.GetRepositoryAsync<ProfilePage>().Update(profile.Id, profile);
            }

            

            return OperationResult.Success();


        }




    }
}
