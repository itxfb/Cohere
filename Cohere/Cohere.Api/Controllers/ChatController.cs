using System;
using System.Threading.Tasks;
using Cohere.Api.Filters;
using Cohere.Api.Utils;
using Cohere.Domain.Models.Chat.WebhookHandling;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Chat;
using Cohere.Entity.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ChatController : CohereController
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        // GET: /Chat/GetToken
        [Authorize]
        [HttpGet("GetToken")]
        public async Task<IActionResult> GetToken()
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var token = await _chatService.GetChatTokenForClientAsync(AccountId);
            if (!string.IsNullOrEmpty(token))
            {
                return Ok(new { token });
            }

            return BadRequest(new ErrorInfo("Unable to get token for user. Please try later and if the problem persists contact support"));
        }

        // POST: /Chat/CreatePersonalChatWith
        [Authorize]
        [HttpPost("CreatePersonalChatWith/{peerUserId}")]
        public async Task<IActionResult> CreatePersonalChatWith(string peerUserId, bool IsOpportunity = false)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            var chatCreationResult = await _chatService.CreatePeerChat(AccountId, peerUserId, IsOpportunity);
            if (chatCreationResult.Succeeded)
            {
                return Ok((PeerChat)chatCreationResult.Payload);
            }

            return BadRequest(new ErrorInfo(chatCreationResult.Message));
        }

        // POST: /Chat/LeavePersonalChat/{chatSid}
        [Authorize]
        [HttpPost("LeavePersonalChat/{chatSid}")]
        public async Task<IActionResult> LeavePersonalChat(string chatSid)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(chatSid))
            {
                return BadRequest(new ErrorInfo("Chat Sid must not be empty"));
            }

            var chatCreationResult = await _chatService.LeavePeerChat(chatSid, AccountId);
            if (chatCreationResult.Succeeded)
            {
                return Ok((PeerChat)chatCreationResult.Payload);
            }

            return BadRequest(new ErrorInfo(chatCreationResult.Message));
        }

        // POST: /Chat/LeaveContributionChat/{contributionId}
        [Authorize]
        [HttpPost("LeaveContributionChat/{contributionId}")]
        public async Task<IActionResult> LeaveContributionChat(string contributionId)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest(new ErrorInfo("Contribution Id must not be empty"));
            }

            var chatCreationResult = await _chatService.LeaveContributionChat(contributionId, AccountId);
            if (chatCreationResult.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo(chatCreationResult.Message));
        }

        // POST: /Chat/SetAsFavorite/{chatSid}
        [Authorize]
        [HttpPost("SetAsFavorite/{chatSid}")]
        public async Task<IActionResult> SetAsFavorite(string chatSid)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(chatSid))
            {
                return BadRequest(new ErrorInfo("Chat Sid must not be empty"));
            }

            var setChatFavoriteResult = await _chatService.ChangeChatFavoriteState(AccountId, chatSid, true);
            if (setChatFavoriteResult.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo(setChatFavoriteResult.Message));
        }
        [Authorize]
        [HttpPost("PinChat")]
        public async Task<IActionResult> PinChat(string chatSid, bool IsPinned)
        {
            if (string.IsNullOrEmpty(chatSid))
            {
                return BadRequest(new ErrorInfo("Chat Sid must not be empty"));
            }
            var pinnedChat = await _chatService.PinChat(chatSid, IsPinned);
            if (pinnedChat.Succeeded)
            {
                return Ok(pinnedChat.Payload);
            }
            return BadRequest(new ErrorInfo(pinnedChat.Message));
        }

        // POST: /Chat/SetAsRegular/{chatSid}
        [Authorize]
        [HttpPost("SetAsRegular/{chatSid}")]
        public async Task<IActionResult> SetAsRegular(string chatSid)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(chatSid))
            {
                return BadRequest(new ErrorInfo("Chat Sid must not be empty"));
            }

            var setChatRegularResult = await _chatService.ChangeChatFavoriteState(AccountId, chatSid, false);
            if (setChatRegularResult.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo(setChatRegularResult.Message));
        }

        // GET: /Chat/GetChatsByType
        [Authorize]
        [HttpGet("GetChatsByType")]
        public async Task<IActionResult> GetChatsByType([FromQuery] string type)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }

            if (string.IsNullOrEmpty(type))
            {
                return BadRequest(new ErrorInfo("Chat type must not be empty"));
            }

            var isCohealer = User.IsInRole(Roles.Cohealer.ToString());

            var chatSids = await _chatService.GetChatSidsByType(AccountId, type, isCohealer);
            if (chatSids != null)
            {
                return Ok(chatSids);
            }

            return BadRequest(new ErrorInfo($"Unable to recognize type {type} of chats to filter"));
        }

        // POST: /Chat/HandleChatEvent
        //[ServiceFilter(typeof(ValidateTwilioRequestAttribute))]
        [HttpPost("HandleChatEvent")]
        [Consumes("application/x-www-form-urlencoded")]
        public IActionResult HandleChatEvent([FromForm] IFormCollection formCollection)
        {
            if (Response.StatusCode == StatusCodes.Status403Forbidden)
            {
                return Forbid();
            }

            if (formCollection == null)
            {
                return BadRequest(new ErrorInfo("Form collection is null"));
            }

            formCollection.TryGetValue("eventType", out StringValues stringValues);
            var eventType = stringValues.ToString();

            ChatEventModel chatEventModel;
            switch (eventType)
            {
                case "onMessageSent":
                    {
                        var isAbleToParse = int.TryParse(formCollection["index"], out var index);

                        chatEventModel = new ChatMessageAddedModel
                        {
                            AccountSid = formCollection["accountSid"],
                            Attributes = formCollection["attributes"],
                            Body = formCollection["body"],
                            ChannelSid = formCollection["channelSid"],
                            ClientIdentity = formCollection["clientIdentity"],
                            DateCreated = DateTime.Parse(formCollection["dateCreated"]),
                            EventType = eventType,
                            From = formCollection["from"],
                            Index = isAbleToParse ? index : (int?)null,
                            InstanceSid = formCollection["instanceSid"],
                            MessageSid = formCollection["messageSid"]
                        };
                        break;
                    }

                case "onMediaMessageSent":
                    {
                        var isAbleToParse = int.TryParse(formCollection["index"], out var index);

                        chatEventModel = new ChatMediaMessageAddedModel
                        {
                            AccountSid = formCollection["accountSid"],
                            Attributes = formCollection["attributes"],
                            Body = formCollection["body"],
                            ChannelSid = formCollection["channelSid"],
                            ClientIdentity = formCollection["clientIdentity"],
                            DateCreated = DateTime.Parse(formCollection["dateCreated"]),
                            EventType = eventType,
                            From = formCollection["from"],
                            Index = isAbleToParse ? index : (int?)null,
                            InstanceSid = formCollection["instanceSid"],
                            MessageSid = formCollection["messageSid"],
                            MediaContentType = formCollection["mediaContentType"],
                            MediaFilename = formCollection["mediaFilename"],
                            MediaSid = formCollection["mediaSid"],
                        };
                        break;
                    }

                case "onMemberUpdated":
                    {
                        var isAbleToParse = int.TryParse(formCollection["lastConsumedMessageIndex"], out var lastConsumedMessageIndex);

                        chatEventModel = new ChatMemberUpdatedModel
                        {
                            AccountSid = formCollection["accountSid"],
                            ChannelSid = formCollection["channelSid"],
                            ClientIdentity = formCollection["clientIdentity"],
                            DateCreated = DateTime.Parse(formCollection["dateCreated"]),
                            DateUpdated = DateTime.Parse(formCollection["dateUpdated"]),
                            EventType = eventType,
                            InstanceSid = formCollection["instanceSid"],
                            LastConsumedMessageIndex = isAbleToParse ? lastConsumedMessageIndex : (int?)null,
                            Identity = formCollection["identity"],
                            MemberSid = formCollection["MemberSid"],
                            RoleSid = formCollection["roleSid"],
                            Source = formCollection["source"]
                        };
                        break;
                    }

                default:
                    return Ok();
            }

            _logger.Log(LogLevel.Information, $"Twilio Chat event received: {eventType}, {chatEventModel.ClientIdentity}, {chatEventModel.InstanceSid}");

            var result = _chatService.HandleChatEvent(chatEventModel);
            if (result.Succeeded)
            {
                return Ok();
            }

            return StatusCode(500, result.Message);
        }
        [Authorize]
        [HttpPost("CreateCustomerSupportChat")]
        public async Task<IActionResult> CreateCustomerSupportChat()
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return BadRequest(new ErrorInfo("Unable to find account Id in bearer token"));
            }
            var chatCreationResult = await _chatService.CreateCustomerSupportChat(AccountId);
            if (chatCreationResult.Succeeded)
            {
                return Ok((CustomerSupportChat)chatCreationResult.Payload);
            }
            return BadRequest(new ErrorInfo(chatCreationResult.Message));
        }
    }
}
