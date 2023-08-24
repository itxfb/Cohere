using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Notification;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IFCMService
    {
        Task<OperationResult> SetUserDeviceToken(string deviceToken, string accountId);
        Task<OperationResult> RemoveUserDeviceToken(string deviceToken, string accountId);
        Task<bool> SendNotificationToUsers(Dictionary<string, string> payload, List<string> userIds, string id, string contributionId, string specificNotification, string senderUserId ,string sessionTitle="", string currencyAmount="" , string title = "", string Message="");
        Task<bool> CheckNotificationPermission(string userId, string _specificNotification);
        Task<OperationResult> ReadNotification(List<string> notificationIds);
        Task<OperationResult> UnreadNotification(List<string> notificationIds);
        Task<OperationResult> RemoveNotification(string notificationId);
        Task<OperationResult> SetNotificationPermission(string accountId, string type, string category, int permission);
        Task<OperationResult> SetDefaultPermissions(string accountId);

        Task<OperationResult> SendPostPushNotification(Post Post);
        Task<OperationResult> SendPinnedPostPushNotification(Post Post,string AccountId);
        Task<OperationResult> SendLikePushNotification(Like Like);
        Task<OperationResult> SendCommentPushNotification(Comment comment);
        Task<OperationResult> SendTaggedUsersPushNotification(UserTaggedNotificationViewModel model, string AccountId);
        Task<OperationResult> SendChatPushNotification(bool IsGroupChat, List<string> MemberEmails, string contributionId, string senderAccountId, string ChannelId, string MessageId, string Massage="");
        Task<OperationResult> SendHourlyReminderPushNotification(string Email, string sessionTimeId, string contributionId, bool oneHourReminder);
        Task<OperationResult> SendRescheduleSessionPushNotification(List<string> UserIds, string contributionId, string sessionId, string sessionTimeId, string sessionTitle);
        Task<OperationResult> SendCancleSessionPushNotification(List<string> UserIds, string sessionTimeId, string contributionId, string sessionId, string sessionTitle);
        Task<OperationResult> SendNewLiveSessionPushNotification(List<string> UserIds, string sessionTimeId, string contributionId, string sessionId, string sessionTitle);
        Task<OperationResult> SendSessionPushNotification(EventDiff eventDiff, string contributionId, string requestUserId, List<string> participantIds);
        Task<OperationResult> SendSelfPacedContentAvailablePushNotification(string sessionId, string sessionTimeId, string contributionId, string requestUserId);
        Task<OperationResult> SetContentAvailableScheduler(ContributionBase contributionBase, string requestAccountId);
        Task<OperationResult> SendPaidOneToOneBookPushNotification(string contributionId, string amountCurrency, string clientUserId);
        Task<OperationResult> SendPaidGroupContributionJoinPushNotification(string contributionId, string amountCurrency, string clientUserId);
        Task<OperationResult> SendFreeGroupContributionJoinPushNotification(string contributionId, string clientUserId);
        Task<OperationResult> SendFreeOneToOneContributionJoinPushNotification(string contributionId, string clientUserId);
        Task<IEnumerable<FcmNotification>> GetAllNotifications(string accountId);
        Task<OperationResult> ReadAllNotifications(string AccountId);
        Task<OperationResult> GetUnreadNotificationsCount(string AccountId);



    }
}
