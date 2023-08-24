using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cohere.Api.Utils;
using Cohere.Domain.Models.Chat;
using Cohere.Domain.Models.Notification;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.FCM;
using Cohere.Domain.Service.FCM.Messaging;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : CohereController
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<ExternalCalendarController> _logger;
        private readonly IFCMService _fcmService;
        private readonly FirebaseSettings _firebaseSettings;

        public NotificationController(INotificationService notificationService, IOptions<FirebaseSettings> firebaseSettings,
            ILogger<ExternalCalendarController> logger,
            IFCMService fcmService)
        {
            _notificationService = notificationService;
            _firebaseSettings = firebaseSettings.Value;
            _logger = logger;
            _fcmService = fcmService;
        }

        [Authorize]
        [HttpPost("NotifyTaggedUsers")]
        public async Task<IActionResult> NotifyTaggedUsers(UserTaggedNotificationViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            await _notificationService.NotifyTaggedUsers(model);

            try
            {
               await _fcmService.SendTaggedUsersPushNotification(model,AccountId);
            }
            catch
            {

            }

            return Ok();
        }

        [HttpPost("FCMNotification")]
        public async Task<IActionResult> TestFCMNotification(string Email, string sessionTimeId, string contributionId, bool oneHourReminder)
        {

            //var dateTimeJobFires = DateTime.UtcNow;

            //var startTime = dateTimeJobFires.AddHours(1);
            //var endime = startTime.AddDays(2);

            //await _notificationService.SendSessionReminders(startTime, endime, true);

            await _fcmService.SendHourlyReminderPushNotification(Email, sessionTimeId, contributionId, oneHourReminder);

            //FCMClient client = new FCMClient(_firebaseSettings.ServerKey);
            //FirebaseAdmin.Messaging.ApnsFcmOptions cc = new FirebaseAdmin.Messaging.ApnsFcmOptions();
            //cc.ImageUrl = "https://source.unsplash.com/user/c_v_r/100x100";
            //Payload payload = new Payload()
            //{
            //    aps = new Aps { MutableContent = 1 }
            //};
            //FcmOptions fcmOptions = new FcmOptions { image = "https://source.unsplash.com/user/c_v_r/100x100" };

            //var message = new TestMessage()
            //{
            //    To = "dWHs0trO80xpk_xo7oFafz:APA91bF-QYD1YjObJ-oVMhV5_BTmyCwLHXqdb7O1Rwc0x9CbYmG1ErQR1BfIWJtPZ8sAI74QyKOoQ4yIoSdetnC26HUdCp-E7ZaxEWi5wcspWUAQHJQeZ9WJzoBulUG1ayuuNOfxlvHr",
            //    notification = new Notification { title = "Local Image", image = "https://source.unsplash.com/user/c_v_r/100x100" },
            //    apns = new Apns { payload = payload, fcm_options = fcmOptions },
            //    Headers = new Dictionary<string, string>
            //    {
            //        { "mutable-content", "1" },

            //    },
            //};
            //var result = await client.SendMessageAsync(message);

            // await _fcmService.SendHourlyReminderPushNotification(clientmail, sessionId, contributionId,oneHourReminder);
            //  await _fcmService.SendNotificationToUsers(new List<string> { AccountId }, "Id", "ContributionId", NotificationType, "Description", AccountId);
            //FCMClient client = new FCMClient("AAAAiC_l--A:APA91bEGeZuW5KU4tYuZWzPNwoO_F9Y0jmRehzH7ei6tsXAsZPRbZ75DC1wkna1RBAOZM1ijB-nYWWqUckz1PfS4x0lpPwjoUGqnr6yK7P9duScs7e3EufQFmo8n2dfF0v9qtg25T-8X");
            //FCMClient client = new FCMClient(_firebaseSettings.ServerKey);

            //var message = new Message()
            //{

            //    To = "dWHs0trO80xpk_xo7oFafz:APA91bF-QYD1YjObJ-oVMhV5_BTmyCwLHXqdb7O1Rwc0x9CbYmG1ErQR1BfIWJtPZ8sAI74QyKOoQ4yIoSdetnC26HUdCp-E7ZaxEWi5wcspWUAQHJQeZ9WJzoBulUG1ayuuNOfxlvHr",
            //    Notification = new AndroidNotification()
            //    {
            //        Body = "Testing notification 2",
            //        Title = "COHERE",
            //        //Icon = "myIcon",
            //        Sound = "Droplets",
            //        Color = "Red",
            //        Tag = "XYZ",
            //        //BodyLocKey= "notification_missed_call_multiple",
            //        //BodyLocArgs = "1",
            //        //ClickAction= "MISSED_CALL",

            //        Image = "https://dev.cohere.live/static/media/logo.e55cae04.png"



            //    },
            //    Priority = MessagePriority.high,
            //    Data = new Dictionary<string, string>
            //    {
            //        { "Nick", "Mario" },
            //        { "body", "great match!" },
            //        { "Room", "PortugalVSDenmark" }
            //    },
            //    DelayWhileIdle = true,


            //};
            //var result = await client.SendMessageAsync(message);
            return Ok();
        }

        [Authorize]
        [HttpPut("ReadNotification")]
        public async Task<IActionResult> ReadNotification(List<string> NotifictionIds)
        {
            if (NotifictionIds.Count==0)
            {
                var errorMessage = $"{NotifictionIds} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.ReadNotification(NotifictionIds);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }
        [Authorize]
        [HttpPut("UnreadNotification")]
        public async Task<IActionResult> UnreadNotification(List<string> NotifictionIds)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                var errorMessage = $"{AccountId} should not be null or empty";
                _logger.LogError(errorMessage);
                return Unauthorized(errorMessage);
            }
            var result = await _fcmService.UnreadNotification(NotifictionIds);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(result.Message);
        }

        [Authorize]
        [HttpPut("RemoveNotification")]
        public async Task<IActionResult> RemoveNotification(string NotifictionId)
        {
            if (string.IsNullOrEmpty(NotifictionId))
            {
                var errorMessage = $"{NotifictionId} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.RemoveNotification(NotifictionId);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }
        [Authorize]
        [HttpPut("UpdateNotificationPermission")]
        public async Task<IActionResult> UpdateNotificationPermission(string type, string category, int permission)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(category))
            {
                var errorMessage = $"{type} and {category} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.SetNotificationPermission(AccountId,type,category,permission);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        } 
        
        [Authorize]
        [HttpPut("SetDefaultPermissions")]
        public async Task<IActionResult> SetDefaultPermissions()
        {
            
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.SetDefaultPermissions(AccountId);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }

        [Authorize]
        [HttpPost("SendChatPushNotification")]
        public async Task<IActionResult> SendChatPushNotification(ChatNotificationAttributes chatNotificationAttribute)
        {
            if (chatNotificationAttribute.MemberEmails.Count==0)
            {
                var errorMessage = $"{chatNotificationAttribute.MemberEmails} should not be null or empty";
                _logger.LogError(errorMessage);
                return BadRequest(errorMessage);
            }
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.SendChatPushNotification(chatNotificationAttribute.IsGroupChat, chatNotificationAttribute.MemberEmails, chatNotificationAttribute.ContributionId, AccountId, chatNotificationAttribute.ChannelSid, chatNotificationAttribute.MessageId,chatNotificationAttribute.Message);
                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
            
        }

        [Authorize]
        [HttpGet("GetAllNotifications")]
        public async Task<IActionResult> GetAllNotifications()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var allnotifications = await _fcmService.GetAllNotifications(AccountId);

                if (allnotifications.Any())
                {
                    return Ok(allnotifications.OrderByDescending(x=>x.CreateTime));
                }

                return Ok(allnotifications);

            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }

        } 
        
        [Authorize]
        [HttpGet("ReadAllNotifications")]
        public async Task<IActionResult> ReadAllNotifications()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.ReadAllNotifications(AccountId);

                if(result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(result.Message);

            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }

        }

        [Authorize]
        [HttpGet("GetUnreadNotificationsCount")]
        public async Task<IActionResult> GetUnreadNotificationsCount()
        {
            try
            {
                if (string.IsNullOrEmpty(AccountId))
                {
                    var errorMessage = $"{AccountId} should not be null or empty";
                    _logger.LogError(errorMessage);
                    return Unauthorized(errorMessage);
                }
                var result = await _fcmService.GetUnreadNotificationsCount(AccountId);
                if (result.Succeeded)
                {
                    return Ok(result.Payload);
                }
                return BadRequest(result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception.Message);
                return StatusCode(500); //Internal server error
            }
        }


    }
}
