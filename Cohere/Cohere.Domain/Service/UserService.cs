using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Service.Generic;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.User;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.Enums.User;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Cohere.Entity.Enums.Account;
using Cohere.Entity.Enums.Contribution;


namespace Cohere.Domain.Service
{
    public class UserService<TViewModel, TEntity> : GenericServiceAsync<TViewModel, TEntity>,
        IUserService<TViewModel, TEntity>
        where TViewModel : UserViewModel
        where TEntity : User
    {
        private readonly IServiceAsync<AccountViewModel, Account> _accountService;
        private readonly INotificationService _notificationService;
        private readonly IContributionService _contributionService;
        private readonly IContributionRootService _contributionRootService;
        private readonly IContentService _contentService;
        private readonly IChatService _chatService;
        private readonly StripeAccountService _stripeAccountService;
        private readonly IServiceAsync<CountryViewModel, Country> _countryService;
        private readonly IServiceAsync<TimeZoneViewModel, TimeZone> _timeZoneService;

        public UserService(
            IUnitOfWork unitOfWork,
            IServiceAsync<AccountViewModel, Account> accountService,
            INotificationService notificationService,
            IContributionService contributionService,
            IContributionRootService contributionRootService,
            IContentService contentService,
            IChatService chatService,
            StripeAccountService stripeAccountService,
            IServiceAsync<CountryViewModel, Country> countryService,
            IServiceAsync<TimeZoneViewModel, TimeZone> timeZoneService,
            IMapper mapper)
            : base(unitOfWork, mapper)
        {
            _accountService = accountService;
            _notificationService = notificationService;
            _stripeAccountService = stripeAccountService;
            _contributionService = contributionService;
            _contributionRootService = contributionRootService;
            _contentService = contentService;
            _chatService = chatService;
            _countryService = countryService;
            _timeZoneService = timeZoneService;
        }

        public override async Task<OperationResult> Insert(TViewModel userVm)
        {
            var accountAssociated =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == userVm.AccountId);

            if (accountAssociated == null)
            {
                return OperationResult.Failure(
                    $"Account with following accountId to associate with is not found {userVm.AccountId}");
            }

            var existedUsers =
                await _unitOfWork.GetRepositoryAsync<User>().Get(u => u.AccountId == accountAssociated.Id);

            if (existedUsers.Any())
            {
                return OperationResult.Failure(
                    $"User associated with following account Id and account Email exists: {userVm.AccountId}, {accountAssociated.Email}");
            }

            if (string.IsNullOrEmpty(userVm.CountryId) && !string.IsNullOrEmpty(userVm.TimeZoneId))
            {
                var allCountries = await _countryService.GetAll();
                var allTimeZones = await _timeZoneService.GetAll();
                if (allCountries?.Count() > 0 && allTimeZones?.Count() > 0)
                {
                    var timeZone = allTimeZones.FirstOrDefault(t => t.CountryName == userVm.TimeZoneId);
                    if (timeZone != null)
                    {
                        var existingCountry = allCountries.FirstOrDefault(c => c.Id == timeZone.CountryId);
                        if (existingCountry != null)
                        {
                            //userVm.CountryId = existingCountry.Id;
                        }
                    }
                }
            }

            var country = await _countryService.GetOne(userVm.CountryId);
            if (country == null && !string.IsNullOrEmpty(userVm.TimeZoneId))
            {
                var allTimeZones = await _timeZoneService.GetAll();
                var timeZone = allTimeZones?.FirstOrDefault(t => t.CountryName == userVm.TimeZoneId);
                if (timeZone != null)
                {
                    var allCountries = await _countryService.GetAll();
                    country = allCountries.FirstOrDefault(c => c.Id == timeZone.CountryId);
                }
            }
            var user = Mapper.Map<User>(userVm);

            if (userVm.IsCohealer && country != null)
            {
                var stripeAcccountResult = await _stripeAccountService.CreateDefaultSripeAccountforUser(accountAssociated.Email, country?.Alpha2Code, user);
                if (stripeAcccountResult.Failed)
                {
                    return stripeAcccountResult;
                }
            }

            if (accountAssociated.Roles.Contains(Roles.Client))
            {
                var createCustomerAccountResult =
                    await _stripeAccountService.CreateCustomerAsync(accountAssociated.Email);
                user.CustomerStripeAccountId = createCustomerAccountResult.Payload;
                // if client's country is supported by stripe, associate account with coheler role id for quick registration
                if (country?.StripeSupportedCountry == true)
                {
                    accountAssociated.Roles.Add(Roles.Cohealer);
                }
            }

            user.BirthDate = userVm.BirthDate.Date;
            user.LanguageCode ??= Constants.LanguageCodeDefault;
            user.TimeZoneId = userVm.TimeZoneId ?? AddTimeZoneInfo(userVm.Location);
            if (user.IsBetaUser) user.ServiceAgreementType = "full";
            var userInserted = await _unitOfWork.GetRepositoryAsync<User>().Insert(user);
            var userInsertedVm = (TViewModel)Mapper.Map<User, UserViewModel>(userInserted);

            // If user added successfully update account with proper OnboardingStatus
            if (userVm.IsCohealer && !accountAssociated.Roles.Contains(Roles.Cohealer))
            {
                accountAssociated.Roles.Add(Roles.Cohealer);
            }

            accountAssociated.OnboardingStatus = OnboardingStatuses.InfoAdded;
            accountAssociated = await _unitOfWork.GetRepositoryAsync<Account>()
                .Update(accountAssociated.Id, accountAssociated);

            // do not sent this notification if accout has signup type other than none
            if (accountAssociated.SignupType == SignupTypes.NONE)
            {
                await _notificationService.SendEmailConfirmationLink(
                accountAssociated.Email,
                accountAssociated.VerificationToken,
                userVm.IsCohealer);
            }

            if (userVm.IsCohealer)
            {
                await _notificationService.NotifyNewCoach(accountAssociated, userInserted);
            }

            var accountAssociatedVm = Mapper.Map<Account, AccountViewModel>(accountAssociated);

            var accountAndUser = new AccountAndUserAggregatedViewModel
            {
                Account = accountAssociatedVm,
                User = userInsertedVm
            };

            return OperationResult.Success("User has been registered", accountAndUser);
        }

        public override async Task<OperationResult> Update(TViewModel userVm)
        {
            var accountAssociated =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == userVm.AccountId);

            if (accountAssociated == null)
            {
                return OperationResult.Failure(
                    $"Account with following accountId to associate with is not found {userVm.AccountId}");
            }

            var userExisting = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userVm.Id);
            var user = Mapper.Map<User>(userVm);
            user.BirthDate = userVm.BirthDate.Date;
            user.LanguageCode ??= Constants.LanguageCodeDefault;
            user.TimeZoneId = userVm.TimeZoneId ?? AddTimeZoneInfo(userVm.Location);
            user.CustomerStripeAccountId = userExisting.CustomerStripeAccountId;
            user.ConnectedStripeAccountId = string.IsNullOrEmpty(userExisting.ConnectedStripeAccountId) ? userVm.ConnectedStripeAccountId : userExisting.ConnectedStripeAccountId;
            user.StripeStandardAccountId = string.IsNullOrEmpty(userExisting.StripeStandardAccountId) ? userVm.StripeStandardAccountId : userExisting.StripeStandardAccountId;
            if (string.IsNullOrWhiteSpace(user.StripeStandardAccountId))
                user.IsStandardAccount = false;
            else
                user.IsStandardAccount = true;
            if (userExisting.IsStandardTaxEnabled)
                user.IsStandardTaxEnabled = true;
            user.PlaidId = userExisting.PlaidId;
            user.TransfersEnabled = userExisting.TransfersEnabled;
            user.TransfersNotLimited = userExisting.TransfersNotLimited;
            user.IsPartnerCoach = userExisting.IsPartnerCoach;
            user.StandardAccountTransfersEnabled = userExisting.StandardAccountTransfersEnabled;
            //user.IsStandardTaxEnabled = userExisting.IsStandardTaxEnabled;
            user.StandardAccountTransfersNotLimited = userExisting.StandardAccountTransfersNotLimited;
            user.PayoutsEnabled = userExisting.PayoutsEnabled;
            user.LastReadSocialInfos = userExisting.LastReadSocialInfos;
            user.ServiceAgreementType = userExisting.ServiceAgreementType;
            user.IsBetaUser = userExisting.IsBetaUser;
            user.OldConnectedStripeAccountId = userExisting.OldConnectedStripeAccountId;
            user.DeviceTokenIds = userExisting.DeviceTokenIds;
            user.NotificationCategories = userExisting.NotificationCategories;
            user.ProfileLinkName = userExisting.ProfileLinkName;
            var userUpdated = await _unitOfWork.GetRepositoryAsync<User>().Update(userVm.Id, user);
            var userInsertedVm = (TViewModel)Mapper.Map<User, UserViewModel>(userUpdated);

            // If user added successfully update account with proper OnboardingStatus
            if (userVm.IsCohealer && !accountAssociated.Roles.Contains(Roles.Cohealer))
            {
                accountAssociated.Roles.Add(Roles.Cohealer);
                accountAssociated = await _unitOfWork.GetRepositoryAsync<Account>()
                    .Update(accountAssociated.Id, accountAssociated);
            }

            var accountAssociatedVm = Mapper.Map<Account, AccountViewModel>(accountAssociated);

            var accountAndUser = new AccountAndUserAggregatedViewModel
            {
                Account = accountAssociatedVm,
                User = userInsertedVm
            };

            return OperationResult.Success("User has been updated", accountAndUser);
        }

        public override async Task<OperationResult> Delete(string id)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == id);
            var accountDeletionResult = await _accountService.Delete(user.AccountId);

            var userContributionsAuthor = await _contributionRootService.Get(c => c.UserId == id);
            var userContributionsAuthorList = userContributionsAuthor.ToList();
            var areAllOwnContributionsDeleted = true;
            if (userContributionsAuthorList.Any())
            {
                var contributionsIds = userContributionsAuthorList.Select(c => c.Id).ToList();
                contributionsIds.ForEach(async cId =>
                {
                    var deletionResult = await _contributionService.Delete(cId);
                    if (!deletionResult.Succeeded)
                    {
                        areAllOwnContributionsDeleted = false;
                    }
                });
            }

            var userPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == id);
            var userSucceededPurchases = userPurchases
                .Where(p => p.Payments.Any(payment => payment.PaymentStatus == PaymentStatus.Succeeded))
                .ToList();
            if (userSucceededPurchases.Any())
            {
                var purchasedContributionsIds = userSucceededPurchases.Select(p => p.ContributionId);
                var userParticipantContributions =
                    await _contributionRootService.Get(c => purchasedContributionsIds.Contains(c.Id));
                var userParticipantContributionVms =
                    Mapper.Map<IEnumerable<ContributionBaseViewModel>>(userParticipantContributions);

                foreach (var userParticipantContributionVm in userParticipantContributionVms)
                {
                    if (userParticipantContributionVm is SessionBasedContributionViewModel)
                    {
                        var podIds = ((SessionBasedContributionViewModel)userParticipantContributionVm).Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                        ((SessionBasedContributionViewModel)userParticipantContributionVm).Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
                    }

                    userParticipantContributionVm.RevokeAllClassesBookedByUseId(id);

                    if (userParticipantContributionVm is ContributionOneToOneViewModel)
                    {
                        var userIdChatSidPair =
                            userParticipantContributionVm.Chat.CohealerPeerChatSids.FirstOrDefault(pair =>
                                pair.Key == id);
                        if (!userIdChatSidPair.Equals(default(KeyValuePair<string, string>)))
                        {
                            var chatSid = userIdChatSidPair.Value;
                            await _chatService.LeavePeerChat(chatSid, user.AccountId);
                        }
                    }

                    var updatedContribution = Mapper.Map<ContributionBase>(userParticipantContributionVm);
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                        .Update(updatedContribution.Id, updatedContribution);
                }
            }

            var avatarDeletionMessage = "No avatar to delete";
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var avatarDeletionResult = await _contentService.DeletePublicImageAsync(user.AvatarUrl);
                avatarDeletionMessage = avatarDeletionResult.Message;
            }

            if (accountDeletionResult.Failed)
            {
                return accountDeletionResult;
            }

            var userDeletionResult = await base.Delete(id);

            if (!areAllOwnContributionsDeleted && userDeletionResult.Succeeded)
            {
                return OperationResult.Failure(
                    $"User with UserId {id} and AccountId {user.AccountId} has bee deleted. Avatar deletion result from storage {avatarDeletionMessage}. BUT!!! not all contributions created bu user were deleted. Please contact support to manually delete them");
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<UserViewModel> GetByAccountIdAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            return Mapper.Map<UserViewModel>(user);
        }

        public async Task<User> GetUserAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            return user;
        }

        public async Task<User> GetUserWithUserId(string userId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userId);
            return user;
        }

        public async Task<Account> GetAccountAsync(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == accountId);
            return account;
        }

        public async Task<User> GetCohealerIconByContributionId(string contributionId)
        {
            if (contributionId == null)
            {
                return null;
            }

            var contribution = await _contributionRootService.GetOne(contributionId);
            return await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == contribution.UserId);
        }

        public async Task<OperationResult> SaveUserSocialLastReadTime(string userId, string contributionId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userId);
            user.LastReadSocialInfos[contributionId] = System.DateTime.UtcNow;

            await _unitOfWork.GetRepositoryAsync<User>().Update(userId, user);

            return OperationResult.Success();
        }

        public async Task<OperationResult> GetCountsOfNonReadedSocialPostsForContributions(string userId, IEnumerable<string> contributionIds)
        {
            var result = new Dictionary<string, long>();
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userId);
            var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();

            foreach (var contributionId in contributionIds)
            {
                if (user.LastReadSocialInfos.ContainsKey(contributionId))
                {
                    var postsCount = await postsRepository.Count(x =>
                        x.ContributionId == contributionId && !x.IsDraft && x.CreateTime > user.LastReadSocialInfos[contributionId]);

                    result.Add(contributionId, postsCount);
                }
            }

            return OperationResult.Success(null, result);
        }

        public async Task<OperationResult> GetTotalCountsOfNonReadedSocialPostsForContributions(string userId, IEnumerable<string> contributionIds)
        {
            var result = new Dictionary<string, long>();
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userId);
            var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
            long totalCount = 0;
            foreach (var contributionId in contributionIds)
            {
                if (user.LastReadSocialInfos.ContainsKey(contributionId))
                {
                    var postsCount = await postsRepository.Count(x =>
                        x.ContributionId == contributionId && !x.IsDraft && x.CreateTime > user.LastReadSocialInfos[contributionId]);
                    totalCount += postsCount;
                }
            }
            result.Add(userId, totalCount);
            return OperationResult.Success(null, result);
        }

        public async Task<OperationResult<UserActivity>> LogUserActivity(string userId)
        {
            var currentUTCTime = System.DateTime.UtcNow;
            var existingUserActivities = await _unitOfWork.GetRepositoryAsync<UserActivity>()
                .Get(x => x.UserId == userId);
            existingUserActivities = existingUserActivities
                .Where(u => u.ActivityTimeUTC.Date == currentUTCTime.Date);
            if (existingUserActivities == null || existingUserActivities?.Count() == 0)
            {
                var createdUserActivity = await _unitOfWork.GetRepositoryAsync<UserActivity>().Insert(new UserActivity()
                {
                    UserId = userId,
                    ActivityTimeUTC = currentUTCTime,
                });
                return OperationResult<UserActivity>.Success("UserActivity record has been created", createdUserActivity);
            }
            return OperationResult<UserActivity>.Success("UserActivity already exists for this day");
        }

        private static string AddTimeZoneInfo(Location location)
        {
            return location == null
                ? Constants.TimeZoneIdDefault
                : DateTimeHelper.CalculateTimeZoneIanaId(location.Latitude, location.Longitude);
        }
        public async Task<OperationResult<Entity.Entities.Messages>> GetPopupMessage()
        {
            int currentDay = System.DateTime.Now.Day;
            var message = new Entity.Entities.Messages();
            int count = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Messages>().GetCount(d => true);
            if (count > 0)
            {
                int priority = currentDay % count;
                message = await _unitOfWork.GetRepositoryAsync<Entity.Entities.Messages>().GetOne(m => m.priority == priority);
            }
            return OperationResult<Cohere.Entity.Entities.Messages>.Success(message);
        }
        public async Task<OperationResult<UserDetailModel>> GetUserDetails(string userId)
        {
            var model = new UserDetailModel();
            var user = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(m => m.Id == userId);
            if (user == null)
            {
                return OperationResult<UserDetailModel>.Failure(
                    $"User with following userId {userId} not found.");
            }
            var country = await _unitOfWork.GetGenericRepositoryAsync<Country>().GetOne(m => m.Id == user.CountryId);
            if (country == null)
            {
                return OperationResult<UserDetailModel>.Failure(
                    $"Country not found against this countryId {user.CountryId}");
            }
            var timeZone = await _unitOfWork.GetGenericRepositoryAsync<TimeZone>().GetOne(m => m.CountryId == user.CountryId);
            if (timeZone == null)
            {
                return OperationResult<UserDetailModel>.Failure(
                    $"TimeZone not found against this countryId {user.CountryId}");
            }
            model.FirstName = user.FirstName;
            model.LastName = user.LastName;
            model.AvatarUrl = user.AvatarUrl;
            model.Bio = user.Bio;
            model.CountryName = country.Name;
            model.TimeZoneId = timeZone.Name;
            model.TimeZoneShortForm = timeZone?.ShortName;
            return OperationResult<UserDetailModel>.Success(model);
        }

        public async Task<OperationResult> AddProfileLinkName(string accountId, string profileLinkName)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(profileLinkName))
            {
                return OperationResult.Failure(string.Empty);
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("User does not exists");
            }
            var isduplicate = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.ProfileLinkName.ToLower() == profileLinkName.ToLower() && x.AccountId != accountId);

            if (isduplicate != null)
            {
                return OperationResult.Failure("Profile name already exists in the system");
            }
            user.ProfileLinkName = profileLinkName;
            await _unitOfWork.GetGenericRepositoryAsync<User>().Update(user.Id, user);
            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> CheckProfileLinkName(string profileLinkName)
        {
            if (string.IsNullOrEmpty(profileLinkName))
            {
                return OperationResult.Failure(string.Empty);
            }

            var isduplicate = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.ProfileLinkName.ToLower() == profileLinkName.ToLower());

            if (isduplicate != null)
            {
                return OperationResult.Success(string.Empty, false);
            }
            return OperationResult.Success(string.Empty, true);
        }
        public async Task<OperationResult> GetClientAndContributionDetailForZapier(string accountId)
        {
            List<UserAndContributionDetailModel> list = new List<UserAndContributionDetailModel>();
            var account = await _unitOfWork.GetGenericRepositoryAsync<Account>().GetOne(m => m.Id == accountId);
            if (account == null)
            {
                return OperationResult.Failure("User doesn't exists.");
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.AccountId == account.Id);
            var purchases = await _unitOfWork.GetGenericRepositoryAsync<Purchase>().Get(m => m.ContributorId == user.Id
            && m.CreateTime >= System.DateTime.UtcNow.AddMonths(-6)
            );

            var filteredPurchases = purchases.Where(m => !m.IsSentToZapier);
            var fiveLatestPurchases = purchases.Where(m => m.IsSentToZapier).OrderByDescending(m => m.CreateTime).Take(5);
            var datasentToZapier = await GetClientAndContributionDetailList(fiveLatestPurchases, false);

            if (filteredPurchases.Any())
            {
                list = await GetClientAndContributionDetailList(filteredPurchases, true);
            }
            list.AddRange(datasentToZapier);
            return OperationResult.Success(string.Empty, list.OrderByDescending(m => m.CreateDateTime).ToList());
        }
        private async Task<List<UserAndContributionDetailModel>> GetClientAndContributionDetailList(IEnumerable<Purchase> purchases, bool updatePurchase)
        {
            List<UserAndContributionDetailModel> list = new List<UserAndContributionDetailModel>();
            var clientIds = purchases?.Select(m => m.ClientId).Distinct().ToList();
            var users = await _unitOfWork.GetRepositoryAsync<User>().Get(m => clientIds.Contains(m.Id));
            var accountIds = users?.Select(m => m.AccountId).ToList().Distinct();
            var accounts = await _unitOfWork.GetRepositoryAsync<Account>().Get(m => accountIds.Contains(m.Id));
            var contributionIds = purchases?.Select(m => m.ContributionId).ToList().Distinct();
            var contributions = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(m => contributionIds.Contains(m.Id));
            foreach (var m in purchases)
            {
                UserAndContributionDetailModel model = new UserAndContributionDetailModel();
                var user = users?.FirstOrDefault(p => p.Id == m.ClientId);
                var contrib = contributions?.FirstOrDefault(p => p.Id == m.ContributionId);
                model.FirstName = user?.FirstName;
                model.LastName = user?.LastName;
                model.AccountId = user?.AccountId;
                model.ClientEmail = accounts?.FirstOrDefault(p => p.Id == model.AccountId)?.Email;
                model.ContributionName = contrib?.Title;
                model.ContributionType = contrib?.Type;
                model.Id = m.Id;
                model.CreateDateTime = m.CreateTime;

                list.Add(model);
                if (updatePurchase)
                {
                    m.IsSentToZapier = true;
                    await _unitOfWork.GetGenericRepositoryAsync<Purchase>().Update(m.Id, m);
                }
            }
            return list;
        }

        public async Task<OperationResult> EnableCustomEmailNotification(string accountId, bool _enableCustomEmailNotification)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return OperationResult.Failure(string.Empty);
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (user == null)
            {
                return OperationResult.Failure("User not exists");
            }

            user.EnableCustomEmailNotification = _enableCustomEmailNotification;
            await _unitOfWork.GetGenericRepositoryAsync<User>().Update(user.Id, user);
            return OperationResult.Success(string.Empty);
        }
        public async Task<OperationResult> UpdateUserProfileColors(string userId, Dictionary<string, string> BrandingColors, string customLogo = "")
        {
            if (string.IsNullOrEmpty(userId))
            {
                return OperationResult.Failure(string.Empty);
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            if (user == null)
            {
                return OperationResult.Failure("User not exists");
            }
            if (!string.IsNullOrEmpty(customLogo))
            {
                user.CustomLogo = customLogo;
            }

            user.BrandingColors = BrandingColors;

            await _unitOfWork.GetGenericRepositoryAsync<User>().Update(user.Id, user);
            return OperationResult.Success(string.Empty, user);
        }
        public bool IsBrandingLogoChanged(User dbUser, UserViewModel modelUser)
        {
            if (modelUser.CustomLogo == null || dbUser.CustomLogo == null)
                return false;
            if (_contentService.GetFileKey(dbUser.CustomLogo) == _contentService.GetFileKey(modelUser.CustomLogo))
                return false;
            return true;
        }
        public bool IsBrandingColorChnaged(User dbUser, UserViewModel modelUser)
        {
            if (dbUser.BrandingColors == null || modelUser.BrandingColors == null)
                return false;
            if (!dbUser.BrandingColors.ContainsKey("AccentColorCode") || !dbUser.BrandingColors.ContainsKey("PrimaryColorCode")
                || (!modelUser.BrandingColors.ContainsKey("AccentColorCode") || !modelUser.BrandingColors.ContainsKey("PrimaryColorCode"))
                || (dbUser.BrandingColors["AccentColorCode"] == modelUser.BrandingColors["AccentColorCode"] && dbUser.BrandingColors["PrimaryColorCode"] == modelUser.BrandingColors["PrimaryColorCode"]))
            {
                return false;
            }
            return true;
        }
        public async Task<OperationResult> GetAndSaveUserProgressbarData(UserViewModel user)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(m => m.Id == user.AccountId);
            Dictionary<string, bool> progressbarData = new Dictionary<string, bool>();
            if (user == null || account == null)
            {
                return OperationResult.Failure("User not found.");
            }
            progressbarData.Add("IsPlanPurchased", false);
            progressbarData.Add("IsProfileUploaded", false);
            progressbarData.Add("IntegrationsDone", false);
            progressbarData.Add("FirstContributionDone", false);
            progressbarData.Add("ContributionPricingDone", false);
            progressbarData.Add("IsBankAccountConnected", false);
            //Setup Payment.
            var purchesPlan = _unitOfWork.GetRepositoryAsync<PaidTierPurchase>().GetOne(m => m.ClientId == user.Id);
            var nylasAccount = _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(m => m.CohereAccountId == user.AccountId);
            var contribution = _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(m => m.UserId == user.Id && m.Status == ContributionStatuses.Approved);
            await Task.WhenAll(purchesPlan, nylasAccount, contribution);
            if (purchesPlan?.Result != null)
            {
                progressbarData["IsPlanPurchased"] = true;
            }
            // Setup Your Profile Page
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                progressbarData["IsProfileUploaded"] = true;
            }
            // Setup your Integrations
            if (nylasAccount?.Result != null || !string.IsNullOrEmpty(account.ZoomRefreshToken) || !string.IsNullOrEmpty(account.ZoomUserId))
            {
                progressbarData["IntegrationsDone"] = true;
            }
            // Create a Contribution
            if (contribution.Result != null)
            {
                progressbarData["FirstContributionDone"] = true;
            }
            // Create a price for contribution
            if (contribution?.Result?.Status == Entity.Enums.Contribution.ContributionStatuses.Approved)
            {
                progressbarData["ContributionPricingDone"] = true;
            }
            // Bank Account connected
            if (account.IsBankAccountConnected)
            {
                progressbarData["IsBankAccountConnected"] = true;
            }
            account.UserProgressbarData = progressbarData;
            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);
            return OperationResult.Success(string.Empty, progressbarData);
        }
        public int GetProgressbarPercentage(Dictionary<string, bool> progressbarData)
        {
            int count = 0;
            int percentage = 0;
            if (progressbarData != null)
            {
                if (progressbarData["IsPlanPurchased"])
                {
                    count++;
                }
                if (progressbarData["IsProfileUploaded"])
                {
                    count++;
                }
                if (progressbarData["IntegrationsDone"])
                {
                    count++;
                }
                if (progressbarData["FirstContributionDone"])
                {
                    count++;
                }
                if (progressbarData["ContributionPricingDone"])
                {
                    count++;
                }
                if (progressbarData["IsBankAccountConnected"])
                {
                    count++;
                }

            }
            return percentage = count > 0 ? 100 * count / 6 : count;
        }

        public async Task<OperationResult> UpdateUserFromDynamicObjectAsync(UserDTO userObject)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userObject.Id);
            if (user != null)
            {
                if (userObject.ClientPreferences != null)
                {
                    user.ClientPreferences = userObject.ClientPreferences;
                }
                if (!string.IsNullOrEmpty(userObject.DefaultPaymentMethod))
                {
                    if (userObject.DefaultPaymentMethod == PaymentTypes.Simple.ToString())
                    {
                        user.DefaultPaymentMethod = PaymentTypes.Simple;

                    }
                    else if (userObject.DefaultPaymentMethod == PaymentTypes.Advance.ToString())
                    {
                        user.DefaultPaymentMethod = PaymentTypes.Advance;
                    }
                }
                if (!string.IsNullOrEmpty(userObject.CountryId))
                {
                    user.CountryId = userObject.CountryId;
                }
                if (!user.IsStandardTaxEnabled && userObject.IsStandardTaxEnabled)
                {
                    user.IsStandardTaxEnabled = userObject.IsStandardTaxEnabled;
                }

                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user, false, false);
                return OperationResult.Success(string.Empty);
            }

            return OperationResult.Failure($"User not exists with this ID: {userObject.Id}");
        }
    }
    public class RoleSwitchingService : IRoleSwitchingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly StripeAccountService _stripeAccountService;
        private readonly INotificationService _notificationService;
        private readonly IServiceAsync<CountryViewModel, Country> _countryService;
        private readonly IServiceAsync<TimeZoneViewModel, TimeZone> _timeZoneService;
        private readonly IActiveCampaignService _activeCampaignService;


        public RoleSwitchingService(
            IUnitOfWork unitOfWork,
            StripeAccountService stripeAccountService,
            INotificationService notificationService,
            IServiceAsync<CountryViewModel, Country> countryService,
            IServiceAsync<TimeZoneViewModel, TimeZone> timeZoneService,
            IActiveCampaignService activeCampaignService)
        {
            _unitOfWork = unitOfWork;
            _stripeAccountService = stripeAccountService;
            _notificationService = notificationService;
            _countryService = countryService;
            _timeZoneService = timeZoneService;
            _activeCampaignService = activeCampaignService;
        }

        public async Task<OperationResult<string>> SwitchFromCoachToClient(string accountId)
        {
            var accountAssociated = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);

            if (accountAssociated == null)
            {
                return OperationResult<string>.Failure(
                    $"Account with following accountId to associate with is not found {accountId}");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountAssociated.Id);

            if (!accountAssociated.Roles.Contains(Roles.Client) || !string.IsNullOrEmpty(user.CustomerStripeAccountId))
            {
                return OperationResult<string>.Success("updating not required", user.CustomerStripeAccountId);
            }

            return OperationResult<string>.Success("Updated", user.CustomerStripeAccountId);
        }

        public async Task<OperationResult> SwitchFromClientToCoach(
            string accountId,
            SwitchFromClientToCoachViewModel model)
        {
            var accountAssociated = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == accountId);
            
            if (accountAssociated == null)
            {
                return OperationResult.Failure(
                    $"Account with following accountId to associate with is not found {accountId}");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountAssociated.Id);
            var userTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == user.TimeZoneId);

            if (accountAssociated.Roles.Contains(Roles.Cohealer) &&
                ((!string.IsNullOrEmpty(user.StripeStandardAccountId) || !string.IsNullOrEmpty(user.ConnectedStripeAccountId))))
            {
                return OperationResult.Success("update not required");
            }

            if (model is null)
            {
                return OperationResult.Failure($"{nameof(model)} is null");
            }

            CountryViewModel country = null;
            if (string.IsNullOrEmpty(user.CountryId) && !string.IsNullOrEmpty(user.TimeZoneId))
            {
                var allCountries = await _countryService.GetAll();
                var allTimeZones = await _timeZoneService.GetAll();
                if (allCountries?.Count() > 0 && allTimeZones?.Count() > 0)
                {
                    var timeZone = allTimeZones.FirstOrDefault(t => t.CountryName == user.TimeZoneId);
                    if (timeZone != null)
                    {
                        var existingCountry = allCountries.FirstOrDefault(c => c.Id == timeZone.CountryId);
                        if (existingCountry != null)
                        {
                            //country = existingCountry;
                            //user.CountryId = existingCountry.Id;
                            // var updateResultResult = await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(user.CountryId))
            {
                // return OperationResult.Failure($"{nameof(user.CountryId)} is null");
            }

            user.BusinessName = model.BusinessName ?? user.BusinessName;
            user.BusinessType = model.BusinessType ?? user.BusinessType;
            user.BusinessType = model.BusinessType ?? BusinessTypes.Coaching;
            user.Certification = model.Certification ?? user.Certification;
            user.Occupation = model.Occupation ?? user.Occupation;
            user.CustomerLabelPreference = model.CustomerLabelPreference ?? user.CustomerLabelPreference;
            user.CustomerLabelPreference = model.CustomerLabelPreference ?? CustomerLabelPreferences.Clients;
            user.TimeZoneId = model.TimeZoneId ?? user.TimeZoneId;
            if (user.IsBetaUser) user.ServiceAgreementType = "full";
            if (country == null)
            {
                country = await _countryService.GetOne(user.CountryId);
            }
            if (country != null)
            {
                var stripeAcccountResult =
                                 await _stripeAccountService.CreateDefaultSripeAccountforUser(
                                     accountAssociated.Email,
                                    country?.Alpha2Code, user);

                if (stripeAcccountResult.Failed)
                {
                    return stripeAcccountResult;
                }

                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);

                if (accountAssociated.AccountPreferences.UserView.Equals("Client"))
                {
                    accountAssociated.AccountPreferences.UserView = "Cohealer";
                }
            }


            if (!accountAssociated.Roles.Contains(Roles.Cohealer))
            {
                accountAssociated.Roles.Add(Roles.Cohealer);
            }

            await _unitOfWork.GetRepositoryAsync<Account>().Update(accountAssociated.Id, accountAssociated);

            await _notificationService.NotifyNewCoach(accountAssociated, user);

            if (string.IsNullOrEmpty(user.CustomerStripeAccountId))
            {
                var createCustomerAccountResult = await _stripeAccountService.CreateCustomerAsync(accountAssociated.Email);
                user.CustomerStripeAccountId = createCustomerAccountResult.Payload;

                if (!accountAssociated.Roles.Contains(Roles.Client))
                {
                    accountAssociated.Roles.Add(Roles.Client);
                    await _unitOfWork.GetRepositoryAsync<Account>().Update(accountAssociated.Id, accountAssociated);
                }
            }

            await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);

            // Account (coach or/and client) is activated
            //var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("US Mountain Standard Time");
            //DateTime nowTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, timeZoneInfo);
            System.DateTime nowTime = System.DateTime.Now;

            var acContact = new ActiveCampaignContact()
            {
                Email = accountAssociated.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
            };

            string cohereAccountType = user.IsCohealer ?
                    EnumHelper<CohereAccountType>.GetDisplayValue(CohereAccountType.CoachAccountActivated) :
                    EnumHelper<CohereAccountType>.GetDisplayValue(CohereAccountType.ClientAccountActivated);


            _activeCampaignService.SendActiveCampaignEvents(acContact, cohereAccountType, nowTime.ToString("MM/dd/yyyy"), user.FirstName + " " + user.LastName);

            return OperationResult.Success("Updated");
        }

    }
}