using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.FCM.Messaging;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.User;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Enums.FCM;
using Cohere.Domain.Extensions;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Domain.Models.Notification;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Entity.Enums.Contribution;
using AutoMapper;

namespace Cohere.Domain.Service.FCM
{
    public class FCMService : IFCMService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly FirebaseSettings _firebaseSettings;
        private readonly IJobScheduler _jobScheduler;
        private readonly IMapper _mapper;
        private readonly ICommonService _commonService;
        public FCMService(IUnitOfWork unitOfWork, IOptions<FirebaseSettings> firebaseSettings,
            IJobScheduler jobScheduler,
            IMapper mapper, ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _firebaseSettings = firebaseSettings.Value;
            _jobScheduler = jobScheduler;
            _mapper = mapper;
            _commonService = commonService;
        }
        public async Task<OperationResult> SetUserDeviceToken(string deviceToken, string accountId)
        {
            if (string.IsNullOrEmpty(deviceToken))
            {
                return OperationResult.Failure(string.Empty);
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            

            if (user == null)
            {
                return OperationResult.Failure(string.Empty);
            }
            if (!user.DeviceTokenIds.Contains(deviceToken))
            {
                user.DeviceTokenIds.Add(deviceToken);
                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            }

            return OperationResult.Success(string.Empty);
        }
         public async Task<OperationResult> RemoveUserDeviceToken(string deviceToken, string accountId)
        {
            if (string.IsNullOrEmpty(deviceToken))
            {
                return OperationResult.Failure(string.Empty);
            }
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            

            if (user == null)
            {
                return OperationResult.Failure(string.Empty);
            }
            if (user.DeviceTokenIds.Contains(deviceToken))
            {
                user.DeviceTokenIds.Remove(deviceToken);
                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<bool> SendNotificationToUsers(Dictionary<string, string> payload, List<string> userIds, string id, string contributionId, string specificNotification, string senderUserId,string sessionName = "", string amountCurrency = "", string title = "", string Message = "")
        {
            //var v1 = CommunityTypeEnum.PostLike.GetCommunityTypeName("Ali", "Contr");
            string contributionTitle = "", sessionTitle="", content="";

            
            var _contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);

            if (_contribution == null)
                contributionTitle = "";
            else
                contributionTitle = _contribution.Title;
            if (!string.IsNullOrEmpty(sessionName))
            {
                sessionTitle = sessionName;
            }
            else
            {
                sessionTitle = contributionTitle + " Session";
            }

            var _sender = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == senderUserId);
            if (_sender == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(_sender.AvatarUrl))
            {
                _sender.AvatarUrl = "https://coherepublic.s3.amazonaws.com/61e57c5af21a47dfdece23ea/807308b8-f030-40c4-a745-64561bc183fb.png";
            }
            foreach (var user in userIds)
            {
                if (await CheckNotificationPermission(user, specificNotification))
                {
                    var _user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == user);
                    var _account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == _user.AccountId);
                    if (!IsPushNotificationEnabled(_account))
                    {
                        continue;
                    }
                    string description = "";
                    if(!string.IsNullOrEmpty(amountCurrency))
                        description = GetNotificationTitle(specificNotification, _sender.FirstName + " " + _sender.LastName, contributionTitle, sessionTitle, content = sessionTitle,amountCurrency);
                    else if (!string.IsNullOrEmpty(sessionTitle))
                        description=GetNotificationTitle(specificNotification, _sender.FirstName + " " + _sender.LastName, contributionTitle,sessionTitle,content=sessionTitle);
                    else
                        description=GetNotificationTitle(specificNotification, _sender.FirstName + " " + _sender.LastName, contributionTitle);

                    if (ChatTypeEnum.GroupMessage.ToString() == specificNotification)
                    {
                        title = contributionTitle;
                    }else if(ChatTypeEnum.DirectMessage.ToString() == specificNotification)
                    {
                        title = $"{_sender.FirstName} {_sender.LastName}";
                    }
                    if(!string.IsNullOrEmpty(Message) && ChatTypeEnum.GroupMessage.ToString() == specificNotification)
                    {
                        description = $"{_sender.FirstName} {_sender.LastName}: {Message}";
                    }else if (!string.IsNullOrEmpty(Message) && ChatTypeEnum.DirectMessage.ToString() == specificNotification)
                    {
                        description = Message;
                    }

                    if (_user.DeviceTokenIds.Any())
                    {
                        //payload.Add("UserId", user);
                        payload.Add("UserType", CheckUserType(_contribution, user));
                        foreach (var devicetokenid in _user.DeviceTokenIds)
                        {
                            FCMClient client = new FCMClient(_firebaseSettings.ServerKey);
                            
                            var message = new Message()
                            {

                                To = devicetokenid,
                                Notification = new AndroidNotification()
                                {
                                    Body = description,
                                    Title = title,
                                    Sound = "tri-tone",
                                    Color = "Red",
                                    Image = _sender.AvatarUrl

                                },
                                Priority = MessagePriority.high,
                                Data = payload,
                                DelayWhileIdle = true,


                            };
                            var result = await client.SendMessageAsync(message);


                        }
                        FcmNotification fcmNotification = new FcmNotification()
                        {
                            Description = description,
                            Title = title,
                            Image= _sender.AvatarUrl,
                            UserType = CheckUserType(_contribution, user),
                            SenderUserId = senderUserId,
                            ReceiverUserId = user,
                            IsRead =0,
                            NotificationInfo = payload
                        };
                        await _unitOfWork.GetRepositoryAsync<FcmNotification>().Insert(fcmNotification);
                        payload.Remove("UserType");
                    }
                }
            }

            return true;
        }

        private bool IsPushNotificationEnabled(Account userAccount)
        {
            return userAccount.IsPushNotificationsEnabled;
        }
        public  string GetNotificationTitle(string specificNotification, string userName="", string contributionName="", string sessionName="", string content="",string amountCurrency="")
        {
            string title = "";
            if (Enum.GetNames(typeof(CommunityTypeEnum)).Contains(specificNotification))
            {
                if(Enum.TryParse<CommunityTypeEnum>(specificNotification, out CommunityTypeEnum CommunityTypeEnum))
                {
                    title = CommunityTypeEnum.GetCommunityTypeName(userName, contributionName);
                }
            }
            else if (Enum.GetNames(typeof(ChatTypeEnum)).Contains(specificNotification))
            {
                if (Enum.TryParse<ChatTypeEnum>(specificNotification, out ChatTypeEnum ChatTypeEnum))
                {
                    title = ChatTypeEnum.GetChatTypeName(userName, contributionName);
                }
            }
            else if (Enum.GetNames(typeof(SessionContentTypeEnum)).Contains(specificNotification))
            {
                if (Enum.TryParse<SessionContentTypeEnum>(specificNotification, out SessionContentTypeEnum SessionContentTypeEnum))
                {
                    title = SessionContentTypeEnum.GetSessionContentTypeName(sessionName,contributionName,userName,content);
                }
            }
            else if (Enum.GetNames(typeof(EnrollmentSaleTypeEnum)).Contains(specificNotification))
            {
                if (Enum.TryParse<EnrollmentSaleTypeEnum>(specificNotification, out EnrollmentSaleTypeEnum EnrollmentSaleTypeEnum))
                {
                    title = EnrollmentSaleTypeEnum.GetEnrollmentSaleTypeName(userName, contributionName,amountCurrency);
                }
            }

            return title;

        }
        public string CheckUserType(ContributionBase contributionBase, string userId)
        {
            string Type = "";
            var coaches = new List<string>();
            if (contributionBase == null)
            {
                return Type;
            }
            coaches.Add(contributionBase.UserId);
            if (contributionBase.Partners.Any())
            {
                var partnerCoaches = contributionBase.Partners.Select(x => x.UserId).ToList();
                coaches.AddRange(partnerCoaches);
            }
            if (coaches.Contains(userId))
            {
                Type = "Cohealer";
            }
            else
            {
                Type = "Client";
            }
            return Type;
        }
        public async Task<bool> CheckNotificationPermission(string userId, string _specificNotification)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            if (user != null)
            {
                var result = user.NotificationCategories.SelectMany(x => x.SpecificNotifications).Where(x => x.Name == _specificNotification).FirstOrDefault();
                if (result!=null)
                {
                    if (result.Permission != 0)
                    {
                        return true;

                    }
                }
            }
            return false;
        }

        public async Task<OperationResult> ReadNotification(List<string> notificationIds)
        {
            if (notificationIds.Count==0)
            {
                return OperationResult.Failure(string.Empty);
            }
            foreach (string NotificationId in notificationIds)
            {
                var fcmNotification = await _unitOfWork.GetRepositoryAsync<FcmNotification>().GetOne(u => u.Id == NotificationId);
                if (fcmNotification == null)
                {
                    continue;
                }
                if (fcmNotification.IsRead != 1)
                {
                    fcmNotification.IsRead = 1;
                    await _unitOfWork.GetRepositoryAsync<FcmNotification>().Update(fcmNotification.Id, fcmNotification);
                }
            }
            

            return OperationResult.Success(string.Empty);
        }
        public async Task<OperationResult> UnreadNotification(List<string> notificationIds)
        {
            if (notificationIds.Count == 0)
            {
                return OperationResult.Failure(string.Empty);
            }
            foreach (string NotificationId in notificationIds)
            {
                var fcmNotification = await _unitOfWork.GetRepositoryAsync<FcmNotification>().GetOne(u => u.Id == NotificationId);
                if (fcmNotification == null)
                {
                    continue;
                }
                if (fcmNotification.IsRead != 0)
                {
                    fcmNotification.IsRead = 0;
                    await _unitOfWork.GetRepositoryAsync<FcmNotification>().Update(fcmNotification.Id, fcmNotification);
                }
            }
            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> ReadAllNotifications(string AccountId)
        {

            var getUserIdByAccountId = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a=>a.AccountId == AccountId);

            if(getUserIdByAccountId == null)
            {
                return OperationResult.Failure("No User Found with AccountId");
            }

            var GetUnreadFcmNotifications = await _unitOfWork.GetRepositoryAsync<FcmNotification>().Get(u => u.ReceiverUserId == getUserIdByAccountId.Id && u.IsRead!=1);

            if(!GetUnreadFcmNotifications.Any())
            {
                return OperationResult.Success(string.Empty);
            }

            await ReadNotification(GetUnreadFcmNotifications.Select(x=>x.Id).ToList());

            return OperationResult.Success(string.Empty);
        }
        public async Task<OperationResult> GetUnreadNotificationsCount(string AccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == AccountId);
            if (user == null)
            {
                return OperationResult.Failure("No User Found with AccountId");
            }
            var GetUnreadFcmNotificationsCount = await _unitOfWork.GetRepositoryAsync<FcmNotification>().Get(u => u.ReceiverUserId == user.Id && u.IsRead != 1);
            return OperationResult.Success(GetUnreadFcmNotificationsCount.Count().ToString());
        }

        public async Task<OperationResult> RemoveNotification(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
            {
                return OperationResult.Failure(string.Empty);
            }
            var fcmNotification = await _unitOfWork.GetRepositoryAsync<FcmNotification>().GetOne(u => u.Id == notificationId);
            if (fcmNotification == null)
            {
                return OperationResult.Success(string.Empty);
            }
            
                await _unitOfWork.GetRepositoryAsync<FcmNotification>().Delete(fcmNotification);
            

            return OperationResult.Success(string.Empty);
        }
        public async Task<IEnumerable<FcmNotification>> GetAllNotifications(string accountId)
        {
            IEnumerable<FcmNotification> fcmNotifications = new List<FcmNotification>();
            if (string.IsNullOrEmpty(accountId))
            {
                return fcmNotifications;
            }
            var requesteduser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            fcmNotifications = await _unitOfWork.GetRepositoryAsync<FcmNotification>().Get(u => u.ReceiverUserId == requesteduser.Id);

            return fcmNotifications;
        }

        public async Task<OperationResult> SetNotificationPermission(string accountId, string Type,string Category, int permission)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(Type) || string.IsNullOrEmpty(Category))
            {
                return OperationResult.Failure(string.Empty);
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (user == null)
            {
                return OperationResult.Failure("User not exists");

            }
            
            bool notificationTypeExists = Enum.GetNames(typeof(CommunityTypeEnum)).Contains(Type) ||
                                          Enum.GetNames(typeof(ChatTypeEnum)).Contains(Type) ||
                                          Enum.GetNames(typeof(SessionContentTypeEnum)).Contains(Type) ||
                                          Enum.GetNames(typeof(EnrollmentSaleTypeEnum)).Contains(Type);
            if (!notificationTypeExists)
            {
                return OperationResult.Failure("Notification type not exists");
            }
            bool categoryTypeExists = Enum.GetNames(typeof(CategoryTypeEnum)).Contains(Category);
            if (!categoryTypeExists)
            {
                return OperationResult.Failure("Category type not exists");
            }
            var notificationCategory = user.NotificationCategories.Where(x => x.Name == Category).FirstOrDefault();
            if(notificationCategory == null)
            {
                user.NotificationCategories.Add(new NotificationCategory { Name = Category });
                notificationCategory = user.NotificationCategories.Where(x => x.Name == Category).FirstOrDefault();
                notificationCategory.SpecificNotifications.Add(new SpecificNotification { Name = Type, Permission = permission });
            }
            else
            {
                var specificNotification = notificationCategory.SpecificNotifications.Where(x => x.Name == Type).FirstOrDefault();
                if(specificNotification == null)
                {
                    notificationCategory.SpecificNotifications.Add(new SpecificNotification { Name = Type , Permission=permission });

                }
                else
                {
                    specificNotification.Permission=permission;
                }
            }

            await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id,user);

            return OperationResult.Success(String.Empty);



        }

        public async Task<OperationResult> SetDefaultPermissions(string AccountId)
        {
           var Categories = Enum.GetNames(typeof(CategoryTypeEnum)).ToList();
            List<string> NotificationTypes = new List<string>();
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == AccountId);
            if (user.IsPermissionsUpdated == false)
            {
                foreach (var category in Categories)
                {
                    List<SpecificNotification> specificNotifications = new List<SpecificNotification>();

                    if (category.ToString() == CategoryTypeEnum.Community.ToString())
                    {
                        NotificationTypes = Enum.GetNames(typeof(CommunityTypeEnum)).ToList();

                        foreach (var notificationType in NotificationTypes)
                        {
                            SpecificNotification obj = new SpecificNotification()
                            {
                                Name = notificationType.ToString(),
                                Permission = (int)PermissionTypeEnum.On

                            };
                            specificNotifications.Add(obj);
                        }
                        user.NotificationCategories.Add(new NotificationCategory { Name = category.ToString(), SpecificNotifications = specificNotifications });

                    }
                    else if (category.ToString() == CategoryTypeEnum.Chat.ToString())
                    {
                        NotificationTypes = Enum.GetNames(typeof(ChatTypeEnum)).ToList();

                        foreach (var notificationType in NotificationTypes)
                        {
                            SpecificNotification obj = new SpecificNotification()
                            {
                                Name = notificationType.ToString(),
                                Permission = (int)PermissionTypeEnum.On

                            };
                            specificNotifications.Add(obj);

                        }
                        user.NotificationCategories.Add(new NotificationCategory { Name = category.ToString(), SpecificNotifications = specificNotifications });

                    }
                    else if (category.ToString() == CategoryTypeEnum.SessionContent.ToString())
                    {

                        NotificationTypes = Enum.GetNames(typeof(SessionContentTypeEnum)).ToList();

                        foreach (var notificationType in NotificationTypes)
                        {
                            int permission = (int)PermissionTypeEnum.On;

                            if (notificationType.ToString() == SessionContentTypeEnum.TwentyFourHourSession.ToString())
                                permission = (int)PermissionTypeEnum.Off;
                            SpecificNotification obj = new SpecificNotification()
                            {
                                Name = notificationType.ToString(),
                                Permission = permission

                            };
                            specificNotifications.Add(obj);

                        }
                        user.NotificationCategories.Add(new NotificationCategory { Name = category.ToString(), SpecificNotifications = specificNotifications });


                    }
                    else if (category.ToString() == CategoryTypeEnum.EnrollmentSale.ToString())
                    {

                        NotificationTypes = Enum.GetNames(typeof(EnrollmentSaleTypeEnum)).ToList();

                        foreach (var notificationType in NotificationTypes)
                        {
                            SpecificNotification obj = new SpecificNotification()
                            {
                                Name = notificationType.ToString(),
                                Permission = (int)PermissionTypeEnum.On

                            };
                            specificNotifications.Add(obj);

                        }
                        user.NotificationCategories.Add(new NotificationCategory { Name = category.ToString(), SpecificNotifications = specificNotifications });

                    }


                }
                user.IsPermissionsUpdated = true;
                await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
            }
            

            return OperationResult.Success(String.Empty);

        }

        #region Push Notification
        public async Task<OperationResult> SendPostPushNotification(Post Post)
        {
           var contributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c=>c.Id==Post.ContributionId);
            var coachPartnerIds = contributionBase.Partners.Select(x=>x.UserId).ToList();
            List<string> userIds = new List<string>();
            IEnumerable<User> users = new List<User>();
            var participants = await GetParticipantsVmsAsync(contributionBase.Id);
            userIds = participants.Select(x => x.Id).ToList();
            //if (contributionBase is SessionBasedContribution existedCourse)
            //{

            //    userIds = existedCourse.Sessions.SelectMany(x => x.SessionTimes).SelectMany(x => x.ParticipantsIds).Distinct().ToList();

            //}else if (contributionBase is ContributionOneToOne oneToOneExistedCourse)
            //{
            //    userIds = oneToOneExistedCourse.AvailabilityTimes.SelectMany(x => x.BookedTimes).Select(x => x.ParticipantId).ToList();
            //}
            userIds.AddRange(coachPartnerIds);
            userIds.Add(contributionBase.UserId);

            //payload
            var payload = new Dictionary<string, string>
                {
                    { "PostId", Post.Id },
                    { "ContributionId", contributionBase.Id },
                    { "NotificationType", CommunityTypeEnum.FirstPost.ToString() }
                };

            users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => userIds.Contains(u.Id) && u.Id != Post.UserId);
            if (users.Any())
            await SendNotificationToUsers(payload,users.Select(x => x.Id).ToList(), Post.Id, contributionBase.Id, CommunityTypeEnum.FirstPost.ToString(), Post.UserId);
            return OperationResult.Success(String.Empty);

        }
          public async Task<OperationResult> SendPinnedPostPushNotification(Post Post, string AccountId)
        {
           var contributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c=>c.Id==Post.ContributionId);
           var requestUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(c=>c.AccountId== AccountId);
            var coachPartnerIds = contributionBase.Partners.Select(x=>x.UserId).ToList();
            List<string> userIds = new List<string>();
            IEnumerable<User> users = new List<User>();
            var participants = await GetParticipantsVmsAsync(contributionBase.Id);
            userIds = participants.Select(x => x.Id).ToList();
            userIds.AddRange(coachPartnerIds);
            userIds.Add(contributionBase.UserId);
            if (userIds.Contains(requestUser.Id))
            {
                userIds.Remove(requestUser.Id);
            }
            //payload
            var payload = new Dictionary<string, string>
                {
                    { "PostId", Post.Id },
                    { "ContributionId", contributionBase.Id },
                    { "NotificationType", CommunityTypeEnum.PinPost.ToString() }
                };

            users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => userIds.Contains(u.Id));
            if (users.Any())
            await SendNotificationToUsers(payload,users.Select(x => x.Id).ToList(), Post.Id, contributionBase.Id, CommunityTypeEnum.PinPost.ToString(), requestUser.Id);
            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendLikePushNotification(Like Like)
        {
            List<string> receiverList = new List<string>();
            Post post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(x => x.Id == Like.PostId);
            if (Like.PostId!=null && Like.CommentId == null)
            {
                //payload
                var payload = new Dictionary<string, string>
                {
                    { "PostId", Like.PostId },
                    { "ContributionId", post.ContributionId },
                    { "NotificationType", CommunityTypeEnum.PostLike.ToString() }
                };
                if (post.UserId != Like.UserId)
                {
                    receiverList.Add(post.UserId);

                    await SendNotificationToUsers(payload,receiverList, Like.Id, post.ContributionId, CommunityTypeEnum.PostLike.ToString(),Like.UserId);

                }

            }else if(Like.PostId != null && Like.CommentId != null)
            {
                Comment comment = await _unitOfWork.GetRepositoryAsync<Comment>().GetOne(x => x.Id == Like.CommentId);
                //payload
                var payload = new Dictionary<string, string>
                {
                    { "PostId", post.Id },
                    { "CommentId", comment.Id },
                    { "ContributionId", post.ContributionId },
                    { "NotificationType", CommunityTypeEnum.CommentLike.ToString() }
                };
                if (comment.UserId != Like.UserId)
                {
                    receiverList.Add(comment.UserId);

                    await SendNotificationToUsers(payload, receiverList, Like.Id, post.ContributionId, CommunityTypeEnum.CommentLike.ToString(), Like.UserId);
                }
                
            }
            return OperationResult.Success(String.Empty);


        }

        public async Task<OperationResult> SendCommentPushNotification(Comment comment)
        {
            if (comment.PostId != null)
            {
                Post post = await _unitOfWork.GetRepositoryAsync<Post>().GetOne(x => x.Id == comment.PostId);

                //payload
                var payload = new Dictionary<string, string>
                {
                    { "PostId", comment.PostId },
                    { "CommentId", comment.Id },
                    { "ContributionId", post.ContributionId },
                    { "NotificationType", CommunityTypeEnum.Comment.ToString() }
                };
                List<string> receiverList = new List<string>();


                if (post.UserId != comment.UserId)
                {
                    receiverList.Add(post.UserId);

                    await SendNotificationToUsers(payload, receiverList, comment.Id, post.ContributionId, CommunityTypeEnum.Comment.ToString(), comment.UserId);
                }

            }
            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendTaggedUsersPushNotification(UserTaggedNotificationViewModel model,string AccountId)
        {
            string contributionId = "", AutherId = "",PostId="";
            if(!string.IsNullOrEmpty(model.AuthorUserId) && !string.IsNullOrEmpty(model.ContributionId) && !string.IsNullOrEmpty(model.PostId))
            {
                contributionId = model.ContributionId;
                AutherId = model.AuthorUserId;
                PostId = model.PostId;
            }
            else
            {
                var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Title == model.ContributionName);
                try
                {
                    var Auther = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId==AccountId);
                    if (contribution != null)
                        contributionId = contribution.Id;
                    if (Auther != null)
                        AutherId = Auther.Id;
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                
            }
            //payload
            var payload = new Dictionary<string, string>
                {
                    { "PostId", PostId },
                    { "ContributionId", contributionId },
                    { "NotificationType", CommunityTypeEnum.Tag.ToString() }
                };
            if (model.MentionedUserIds.Contains(AutherId))
                model.MentionedUserIds.Remove(AutherId);
            await SendNotificationToUsers(payload, model.MentionedUserIds,PostId, contributionId, CommunityTypeEnum.Tag.ToString(), AutherId);
            return OperationResult.Success(String.Empty);
        }


        public async Task<OperationResult> SendChatPushNotification (bool IsGroupChat, List<string> MemberEmails, string contributionId, string senderAccountId, string ChannelId, string MessageId, string Message="")
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == senderAccountId);
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == senderAccountId);

            string sepecificNotification = "";
            if (IsGroupChat)
                sepecificNotification = ChatTypeEnum.GroupMessage.ToString();
            else
                sepecificNotification = ChatTypeEnum.DirectMessage.ToString();

            if (MemberEmails.Contains(account.Email))
            {
                MemberEmails.Remove(account.Email);
            }
            var accounts = await _unitOfWork.GetRepositoryAsync<Account>().Get(u => MemberEmails.Contains(u.Email));
            var accountIds = accounts.Select(x => x.Id);
            var users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => accountIds.Contains(u.AccountId));


            //payload
            var payload = new Dictionary<string, string>
                {
                    
                    { "ContributionId", contributionId },
                    { "ChannelSid", ChannelId },
                    { "MessageId", MessageId },
                    { "NotificationType", sepecificNotification }
                };
            await SendNotificationToUsers(payload, users.Select(x=>x.Id).ToList(),"", contributionId, sepecificNotification, user.Id,"","","",Message);

            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendHourlyReminderPushNotification(string Email, string sessionTimeId, string contributionId, bool oneHourReminder)
        {
            if(Email != null)
            {
                string sessionTitle = "";
                var sessionId = "";
                // SessionTimeToSession sessionTimeToSession = 
                Account account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Email.ToLower() == Email.ToLower());
                if (account != null)
                {
                    ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
                    if (contributionbase is SessionBasedContribution existedCourse)
                    {
                        try
                        {
                            // var session = existedCourse.Sessions.Where(x => x.SessionTimes.Where(x => x.Id == sessionTimeId).FirstOrDefault().Id == sessionTimeId).FirstOrDefault();
                            var session = existedCourse.GetSessionBySessionTimeId(sessionTimeId);
                            if (session != null)
                            {
                                sessionTitle = session.Title;
                                sessionId = session.Id;
                            }
                            else
                            {
                                sessionTitle = contributionbase.Title + " Session";
                            }
                        }
                        catch
                        {
                            sessionTitle = contributionbase.Title + " Session";
                        }

                    }
                    else
                    {
                         sessionTitle = contributionbase.Title + " Session";
                    }


                    string sepecificNotification = "";
                    if (oneHourReminder)
                        sepecificNotification = SessionContentTypeEnum.OneHourSession.ToString();
                    else
                        sepecificNotification = SessionContentTypeEnum.TwentyFourHourSession.ToString();
                    //payload
                    var payload = new Dictionary<string, string>
                    {
                        { "SessionId", sessionId },
                        { "SessionTimeId", sessionTimeId },
                        { "ContributionId", contributionId },
                        { "NotificationType", sepecificNotification }
                    };
                    var receiver = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == account.Id);
                  
                    await SendNotificationToUsers(payload, new List<string> { receiver.Id}, sessionTimeId, contributionId, sepecificNotification, receiver.Id,sessionTitle);
                }


            }
            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendRescheduleSessionPushNotification(List<string> UserIds, string contributionId,string sessionId, string sessionTimeId,string sessionTitle)
        {
                ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            //payload
            var payload = new Dictionary<string, string>
                    {
                        { "SessionTimeId", sessionTimeId },
                        { "SessionId", sessionId },
                        { "ContributionId", contributionId },
                        { "NotificationType", SessionContentTypeEnum.RescheduledSession.ToString() }
                    };
            await SendNotificationToUsers(payload, UserIds, sessionTimeId, contributionId, SessionContentTypeEnum.RescheduledSession.ToString(), contributionbase.UserId,sessionTitle);
                
            return OperationResult.Success(String.Empty);

        }
        public async Task<OperationResult> SendCancleSessionPushNotification(List<string> UserIds, string sessionTimeId, string contributionId, string sessionId, string sessionTitle)
        {
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            //payload
            var payload = new Dictionary<string, string>
                    {
                        { "SessionTimeId", sessionTimeId },
                        { "SessionId", sessionId },
                        { "ContributionId", contributionId },
                        { "NotificationType", SessionContentTypeEnum.CanceledSession.ToString() }
                    };
            await SendNotificationToUsers(payload, UserIds, sessionTimeId, contributionId, SessionContentTypeEnum.CanceledSession.ToString(), contributionbase.UserId,sessionTitle);

            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendNewLiveSessionPushNotification(List<string> UserIds, string sessionTimeId, string contributionId, string sessionId, string sessionTitle)
        {
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            //payload
            var payload = new Dictionary<string, string>
                    {
                        { "SessionTimeId", sessionTimeId },
                        { "SessionId", sessionId },
                        { "ContributionId", contributionId },
                        { "NotificationType", SessionContentTypeEnum.NewLiveSession.ToString() }
                    };
            await SendNotificationToUsers(payload, UserIds, sessionTimeId, contributionId, SessionContentTypeEnum.NewLiveSession.ToString(), contributionbase.UserId, sessionTitle);

            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SendSelfPacedNewContentPushNotification(List<string> UserIds, string sessionTimeId, string contributionId, string sessionId, string sessionTitle)
        {
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            //payload
            var payload = new Dictionary<string, string>
                    {
                        { "SessionTimeId", sessionTimeId },
                        { "SessionId", sessionId },
                        { "ContributionId", contributionId },
                        { "NotificationType", SessionContentTypeEnum.NewSelfPacedAvailable.ToString() }
                    };
            await SendNotificationToUsers(payload, UserIds, sessionTimeId, contributionId, SessionContentTypeEnum.NewSelfPacedAvailable.ToString(), contributionbase.UserId, sessionTitle);

            return OperationResult.Success(String.Empty);

        }
           public async Task<OperationResult> SendSelfPacedContentAvailablePushNotification(string sessionId, string sessionTimeId, string contributionId, string requestUserId)
        {
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            //payload
            var payload = new Dictionary<string, string>
                    {
                        { "SessionTimeId", sessionTimeId },
                        { "SessionId", sessionId },
                        { "ContributionId", contributionId },
                        { "NotificationType", SessionContentTypeEnum.SelfPacedAvailable.ToString() }
                    };
            if (contributionbase != null)
            {
                var partnerCoaches = contributionbase.Partners.Select(x => x.UserId).ToList();
                if(contributionbase is SessionBasedContribution sessionBasedContribution)
                {
                    //var session = sessionBasedContribution.GetSessionTimes()?.Where(m => m.Value.SessionTime.Id == sessionTimeId).FirstOrDefault();
                    var session = sessionBasedContribution.GetSessionBySessionTimeId(sessionTimeId);
                    var participantIds = await GetParticipantsVmsAsync(contributionId);
                    List<string> userList = new List<string>();
                    userList.AddRange(partnerCoaches);
                    userList.Add(contributionbase.UserId);
                    userList.AddRange(participantIds.Select(x => x.Id).ToList());
                    if (userList.Contains(requestUserId))
                        userList.Remove(requestUserId);
                    if(userList.Count>0)
                        await SendNotificationToUsers(payload, userList, sessionTimeId, contributionId, SessionContentTypeEnum.SelfPacedAvailable.ToString(), requestUserId, session?.Name);
                }

                
            }
            return OperationResult.Success(String.Empty);

        }
        public async Task<List<ParticipantViewModel>> GetParticipantsVmsAsync(string contributionId, User user = null)
        {
            var allPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributionId == contributionId);
            var allPurchasesVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(allPurchases).ToList();
            foreach (var participantPurchaseVm in allPurchasesVms)
            {
                if ((user == null || participantPurchaseVm.ClientId == user.Id))
                {
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
                    var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
                    participantPurchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
                }
            }

            var participantsWithAccess = allPurchasesVms
                .Where(p => p.HasAccessToContribution)
                .ToList();

            var trialParticipants = participantsWithAccess
                .Where(e => e.RecentPaymentOption == PaymentOptions.Trial)
                .Select(e => e.ClientId)
                .ToHashSet();

            var participantsIds = participantsWithAccess
                .Select(p => p.ClientId)
                .ToList();

            if (participantsIds.Count <= 0)
            {
                return new List<ParticipantViewModel>();
            }

            var participants = await _unitOfWork.GetRepositoryAsync<User>().Get(u => participantsIds.Contains(u.Id));
            var participantsDict = _mapper.Map<List<ParticipantViewModel>>(participants).ToDictionary(e => e.Id);

            var addedByAccessCode = participantsDict.Keys.Intersect(trialParticipants);

            foreach (var participantId in addedByAccessCode)
            {
                participantsDict[participantId].IsAddedByAccessCode = true;
            }

            return participantsDict.Values.ToList();
        }
        public async Task<OperationResult> SendSessionPushNotification(EventDiff eventDiff, string contributionId, string requestUserId, List<string> participantIds)
        {
            if (eventDiff != null)
            {
                ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
                if (contributionbase != null)
                {
                    var partnerCoaches = contributionbase.Partners.Select(x => x.UserId).ToList();

                    if (eventDiff.UpdatedEvents.Any())
                    {

                        foreach (var eventD in eventDiff.UpdatedEvents)
                        {
                            if (!eventD.Session.IsPrerecorded)
                            {
                                List<string> userList = new List<string>();
                                userList.AddRange(partnerCoaches);
                                userList.Add(contributionbase.UserId);
                                userList.AddRange(eventD.SessionTime.ParticipantsIds.ToList());
                                if (userList.Contains(requestUserId))
                                    userList.Remove(requestUserId);
                                await SendRescheduleSessionPushNotification(eventD.SessionTime.ParticipantsIds.ToList(), contributionId, eventD.Session.Id, eventD.SessionTime.Id, eventD.Session.Name);

                            }
                            else
                            {
                                if (eventD.SessionTime.IgnoreDateAvailable)
                                {
                                    DateTime now = DateTime.UtcNow;
                                    DateTime scheduletime = Convert.ToDateTime(eventD.SessionTime.StartTime);
                                    if (now < scheduletime)
                                    {
                                        TimeSpan dff = (scheduletime.Subtract(now));
                                        if (contributionbase is SessionBasedContribution sessionBasedContribution)
                                        {
                                            var session = sessionBasedContribution.GetSessionBySessionTimeId(eventD.SessionTime.Id);
                                            string jobId = "";
                                            if (session != null)
                                            {
                                                var sessiontime = session.SessionTimes.Where(x => x.Id == eventD.SessionTime.Id).FirstOrDefault();
                                                if (!string.IsNullOrEmpty(sessiontime.ScheduledNotficationJobId))
                                                {
                                                    jobId = _jobScheduler.UpdateScheduleJob<IContentAvailableJob>(sessiontime.ScheduledNotficationJobId,dff, eventD.Session.Id, eventD.SessionTime.Id, contributionbase.Id, requestUserId);
                                                    sessiontime.ScheduledNotficationJobId = jobId;
                                                }
                                                else
                                                {
                                                    jobId = _jobScheduler.ScheduleJob<IContentAvailableJob>(dff, eventD.Session.Id, eventD.SessionTime.Id, contributionbase.Id, requestUserId);
                                                    sessiontime.ScheduledNotficationJobId = jobId;
                                                }
                                                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contributionbase);


                                            }
                                        }
                                        //await SendSelfPacedContentAvailablePushNotification(eventD.Session.Id, eventD.SessionTime.Id, contributionbase.Id, requestUserId);
                                    }
                                }
                                else
                                {
                                    if (contributionbase is SessionBasedContribution sessionBasedContribution)
                                    {
                                        var session = sessionBasedContribution.GetSessionBySessionTimeId(eventD.SessionTime.Id);
                                        if (session != null)
                                        {
                                            var sessiontime = session.SessionTimes.Where(x => x.Id == eventD.SessionTime.Id).FirstOrDefault();
                                            if (!string.IsNullOrEmpty(sessiontime.ScheduledNotficationJobId))
                                            {
                                                bool result = _jobScheduler.DeleteScheduleJob<IContentAvailableJob>(sessiontime.ScheduledNotficationJobId);
                                                sessiontime.ScheduledNotficationJobId = null;
                                                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contributionbase);
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    }

                    if (eventDiff.CanceledEvents.Any())
                    {
                        foreach (var eventD in eventDiff.CanceledEvents)
                        {
                            if (!eventD.Session.IsPrerecorded)
                            {
                                List<string> userList = new List<string>();
                                userList.AddRange(partnerCoaches);
                                userList.Add(contributionbase.UserId);
                                userList.AddRange(eventD.SessionTime.ParticipantsIds.ToList());
                                if (userList.Contains(requestUserId))
                                    userList.Remove(requestUserId);
                                await SendCancleSessionPushNotification(userList, eventD.SessionTime.Id, contributionbase.Id, eventD.Session.Id, eventD.Session.Name);
                            }
                        }

                    }
                    if (eventDiff.CreatedEvents.Any())
                    {
                            List<string> userList = new List<string>();
                            userList.AddRange(partnerCoaches);
                            userList.Add(contributionbase.UserId);
                            userList.AddRange(participantIds);
                        if (userList.Contains(requestUserId))
                            userList.Remove(requestUserId);
                        foreach (var eventD in eventDiff.CreatedEvents)
                            {
                                if (eventD.Session.IsPrerecorded)
                                {
                                    await SendSelfPacedNewContentPushNotification(userList, eventD.SessionTime.Id, contributionbase.Id, eventD.Session.Id, eventD.Session.Name);
                                    if (eventD.SessionTime.IgnoreDateAvailable)
                                    {
                                        DateTime now = DateTime.UtcNow;
                                        DateTime scheduletime = Convert.ToDateTime(eventD.SessionTime.StartTime);
                                        if (now < scheduletime)
                                        {
                                            TimeSpan dff = (scheduletime.Subtract(now));
                                            if (contributionbase is SessionBasedContribution sessionBasedContribution)
                                            {
                                                var session = sessionBasedContribution.GetSessionBySessionTimeId(eventD.SessionTime.Id);

                                                if (session != null)
                                                {
                                                    var sessiontime = session.SessionTimes.Where(x => x.Id == eventD.SessionTime.Id).FirstOrDefault();
                                                    string jobId = _jobScheduler.ScheduleJob<IContentAvailableJob>(dff, eventD.Session.Id, eventD.SessionTime.Id, contributionbase.Id, requestUserId);
                                                    sessiontime.ScheduledNotficationJobId = jobId;
                                                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contributionbase);

                                                }
                                            }
                                            //await SendSelfPacedContentAvailablePushNotification(eventD.Session.Id, eventD.SessionTime.Id, contributionbase.Id, requestUserId);
                                        }
                                    }
                                }
                                else
                                {
                                    await SendNewLiveSessionPushNotification(userList, eventD.SessionTime.Id, contributionbase.Id, eventD.Session.Id, eventD.Session.Name);
                                }
                            }

                        }

                }
              
            }
            return OperationResult.Success(String.Empty);

        }

        public async Task<OperationResult> SetContentAvailableScheduler(ContributionBase contributionBase, string requestAccountId)
        {
            if(contributionBase is SessionBasedContribution sessionBasedContribution)
            {
                var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == requestAccountId);
                var sessions = sessionBasedContribution.GetSessionTimes($"{contributorUser.FirstName} {contributorUser.LastName}", true).Where(x => x.Value.SessionTime.IgnoreDateAvailable);
                foreach(var session in sessions)
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime scheduletime = Convert.ToDateTime(session.Value.SessionTime.StartTime);
                    if (now < scheduletime)
                    {
                        TimeSpan dff = (scheduletime.Subtract(now));
                        string jobId = _jobScheduler.ScheduleJob<IContentAvailableJob>(dff, session.Value.Session.Id, session.Value.SessionTime.Id, sessionBasedContribution.Id, contributorUser.Id.ToString());
                        session.Value.SessionTime.ScheduledNotficationJobId = jobId;
                        await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionBase.Id, contributionBase);
                    }
                }
            }
            return OperationResult.Success(String.Empty);
        }

        public async Task<OperationResult> SendPaidOneToOneBookPushNotification(string contributionId, string amountCurrency, string clientUserId)
        {
            var payload = new Dictionary<string, string>
                    {
                        { "ContributionId", contributionId },
                        { "NotificationType", EnrollmentSaleTypeEnum.BookPaidSession.ToString() }
                    };
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == clientUserId);
            if (contributionbase != null)
            {
                List<string> userList = new List<string>();
                userList.Add(contributionbase.UserId);
                //send to Coach 
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.BookPaidSession.ToString(), clientUser.Id,"",amountCurrency);
                //send to client
                userList.Remove(contributionbase.UserId);
                userList.Add(clientUser.Id);
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.ClientBooked.ToString(), clientUser.Id);
            }
            return OperationResult.Success(String.Empty);
        }
        public async Task<OperationResult> SendPaidGroupContributionJoinPushNotification(string contributionId, string amountCurrency, string clientUserId)
        {
            var payload = new Dictionary<string, string>
                    {
                        { "ContributionId", contributionId },
                        { "NotificationType", EnrollmentSaleTypeEnum.JoinPaidContribution.ToString() }
                    };
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == clientUserId);
            if (contributionbase != null)
            {
                List<string> userList = new List<string>();
                userList.Add(contributionbase.UserId);
                //send to Coach 
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.JoinPaidContribution.ToString(), clientUser.Id,"",amountCurrency);
                //send to client
                userList.Remove(contributionbase.UserId);
                userList.Add(clientUser.Id);
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.ClientBooked.ToString(), clientUser.Id);
            }
            return OperationResult.Success(String.Empty);
        }
        public async Task<OperationResult> SendFreeGroupContributionJoinPushNotification(string contributionId, string clientUserId)
        {
            var payload = new Dictionary<string, string>
                    {
                        { "ContributionId", contributionId },
                        { "NotificationType", EnrollmentSaleTypeEnum.JoinFreeContribution.ToString() }
                    };
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == clientUserId);

            //END
            if (contributionbase != null)
            {
                List<string> userList = new List<string>();
                userList.Add(contributionbase.UserId);
                //send to Coach 
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.JoinFreeContribution.ToString(), clientUser.Id);
                //send to client
                userList.Remove(contributionbase.UserId);
                userList.Add(clientUser.Id);
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.ClientBooked.ToString(), clientUser.Id);
            }
            return OperationResult.Success(String.Empty);
        }

        public async Task<OperationResult> SendFreeOneToOneContributionJoinPushNotification(string contributionId, string clientUserId)
        {
            var payload = new Dictionary<string, string>
                    {
                        { "ContributionId", contributionId },
                        { "NotificationType", EnrollmentSaleTypeEnum.BookFreeSession.ToString() }
                    };
            ContributionBase contributionbase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == clientUserId);

            //END
            if (contributionbase != null)
            {
                List<string> userList = new List<string>();
                userList.Add(contributionbase.UserId);
                //send to Coach 
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.BookFreeSession.ToString(), clientUser.Id);
                //send to client
                userList.Remove(contributionbase.UserId);
                userList.Add(clientUser.Id);
                await SendNotificationToUsers(payload, userList, "", contributionId, EnrollmentSaleTypeEnum.ClientBooked.ToString(), clientUser.Id);
            }
            return OperationResult.Success(String.Empty);
        }

        #endregion
    }
}
