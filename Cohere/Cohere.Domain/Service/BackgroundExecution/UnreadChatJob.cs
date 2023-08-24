using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class UnreadChatJob : IUnreadChatJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ILogger<UnreadChatJob> _logger;

        public UnreadChatJob(
                IUnitOfWork unitOfWork,
                INotificationService notificationService,
                ILogger<UnreadChatJob> logger)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }

        public void Execute(params object[] args)
        {

            try
            {
                _logger.Log(LogLevel.Information, $"Started {nameof(UnreadChatJob)} at {DateTime.UtcNow}");
                var usersToSend = GetUserEmailsToSendNotification();
                _logger.Log(LogLevel.Information, $"{nameof(UnreadChatJob)} users to send list count {usersToSend.Count}");
                    if (usersToSend.Count > 0)
                    {
                        _notificationService.SendUnreadConversationsNotification(usersToSend);
                    }
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, $"UnreadChatJob at {DateTime.UtcNow}", e.Message);
            }

        }

        private HashSet<string> GetUserEmailsToSendNotification()
        {
            // Need unique emails only, so use HashSet instead of List
            var emailsToSendNotifications = new HashSet<string>();

            using (MachineLock.Create(_unitOfWork.GetHashCode().ToString(),
                TimeSpan.FromMilliseconds(Constants.Chat.TimeToWaitTaskCompletedMilliseconds),
                Constants.Chat.NumberOfRetry))
            {
                var unreadGroupChats = _unitOfWork.GetRepositoryAsync<ChatConversation>()
                    .Get(c => c.HasUnread).GetAwaiter().GetResult();

                foreach (var chat in unreadGroupChats)
                {
                    bool chatHasUsersToNotify = false;

                    foreach (var user in chat.UserReadInfos)
                    {
                        var userHasUnread = (chat.LastMessageIndex != null && user.LastReadMessageIndex == null) ||
                                            (user.LastReadMessageIndex.HasValue && chat.LastMessageIndex.HasValue && user.LastReadMessageIndex < chat.LastMessageIndex);
                        var needToSendFirstNotification = (DateTime.UtcNow - chat.LastMessageAddedTimeUtc).TotalMinutes > Constants.Chat.SendFirstUnreadNotificationInMinutes;
                        var firstNotificationHasBeenSent = user.FirstNotificationSentUtc != default;
                        var needToSendSecondNotification = (DateTime.UtcNow - chat.LastMessageAddedTimeUtc).TotalDays > Constants.Chat.SendSecondUnreadNotificationInDays;
                        var secondNotificationHasBeenSent = user.SecondNotificationSentUtc != default;

                        bool isAddedToList = false;
                        if (userHasUnread && needToSendFirstNotification && !firstNotificationHasBeenSent)
                        {
                            isAddedToList = true;
                            chatHasUsersToNotify = true;
                            emailsToSendNotifications.Add(user.Email);
                            user.FirstNotificationSentUtc = DateTime.UtcNow;
                        }

                        if (!isAddedToList && userHasUnread && needToSendSecondNotification &&
                            !secondNotificationHasBeenSent)
                        {
                            chatHasUsersToNotify = true;
                            emailsToSendNotifications.Add(user.Email);
                            user.SecondNotificationSentUtc = DateTime.UtcNow;
                        }
                    }

                    if (chatHasUsersToNotify)
                    {
                        _unitOfWork.GetRepositoryAsync<ChatConversation>().Update(chat.Id, chat).GetAwaiter().GetResult();
                    }
                }
            }

            return emailsToSendNotifications;
        }
    }
}
