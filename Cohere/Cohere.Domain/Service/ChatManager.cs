using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Chat;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cohere.Entity.EntitiesAuxiliary.Chat;
using Cohere.Entity.Enums;
using Twilio;
using Twilio.Exceptions;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Chat.V2.Service;
using Twilio.Rest.Chat.V2.Service.Channel;
using Twilio.Rest.Conversations.V1.Service;
using Twilio.Rest.Conversations.V1.Service.Conversation;
using Twilio.Converters;

namespace Cohere.Domain.Service
{
    public class ChatManager : IChatManager
    {
        private readonly ILogger<ChatManager> _logger;
        private readonly string _accountSid;
        private readonly string _apiSid;
        private readonly string _apiSecret;
        private readonly string _chatServiceSid;
        private readonly string _chatUserRoleSid;
        private readonly int _chatTokenLifetimeSec;

        public ChatManager(
            ILogger<ChatManager> logger,
            string accountSid,
            string apiSid,
            string apiSecret,
            string authToken,
            string chatServiceSid,
            string chatUserRoleSid,
            int chatTokenLifetimeSec)
        {
            _logger = logger;
            _accountSid = accountSid;
            _apiSid = apiSid;
            _apiSecret = apiSecret;
            _chatServiceSid = chatServiceSid;
            _chatUserRoleSid = chatUserRoleSid;
            _chatTokenLifetimeSec = chatTokenLifetimeSec;
            TwilioClient.Init(_accountSid, authToken);
        }

        public string GetToken(string identityName)
        {
            var grants = new HashSet<IGrant>
                {
                    new ChatGrant { ServiceSid = _chatServiceSid }
                };

            var token = new Token(
                _accountSid,
                _apiSid,
                _apiSecret,
                identity: identityName,
                expiration: DateTime.UtcNow.AddSeconds(_chatTokenLifetimeSec),
                nbf: DateTime.UtcNow,
                grants: grants);

            return token.ToJwt();
        }

        public async Task<OperationResult> CreateContributionChatChannelAsync(GroupChat chat, string contributionId)
        {
            var channelAttributes = new ContributionChatAttributes
            {
                Type = ChatTypes.ContributionChat,
                PreviewImage = chat.PreviewImageUrl,
                ContributionId = contributionId,
            };

            var channelOptions = new CreateConversationOptions(_chatServiceSid)
            {
                FriendlyName = chat.FriendlyName,
                UniqueName = contributionId,
                // Type = ChannelResource.ChannelTypeEnum.Private,
                Attributes = JsonSerializer.Serialize(channelAttributes)
            };

            try
            {
                var channel = await ConversationResource.CreateAsync(channelOptions);
                chat.Sid = channel.Sid;
                try
                {
                    List<string> permissions = new List<string>()
                    {
                        "editAnyMessage",
                        "editAnyMessageAttributes"
                    };
                    //var RoleOptions = new Twilio.Rest.Conversations.V1.UpdateRoleOptions(channel.Sid, permission: Promoter.ListOfOne("editAnyMessage"));
                    var RoleOptions = new Twilio.Rest.Conversations.V1.UpdateRoleOptions(channel.Sid, permissions);
                    await Twilio.Rest.Conversations.V1.RoleResource.UpdateAsync(RoleOptions);
                }catch(ApiException ex)
                {
                    _logger.Log(LogLevel.Error, $"ChatService.CreateGroupChatChannel Permission update method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                }

            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.CreateGroupChatChannel method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to create the group chat, the Vendor error occured {ex.Message}");
            }

            return OperationResult.Success(string.Empty, chat);
        }

        public async Task<OperationResult> UpdateGroupChatChannelAsync(GroupChat chat, string contributionId)
        {
            try
            {
                var channel = await ConversationResource.FetchAsync(_chatServiceSid, chat.Sid);

                var channelAttributes = JsonSerializer.Deserialize<ContributionChatAttributes>(channel.Attributes);
                channelAttributes.PreviewImage = chat.PreviewImageUrl;

                var channelOptions = new UpdateConversationOptions(_chatServiceSid, chat.Sid)
                {
                    FriendlyName = chat.FriendlyName,
                    UniqueName = contributionId,
                    Attributes = JsonSerializer.Serialize(channelAttributes)
                };

                await ConversationResource.UpdateAsync(channelOptions);
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.UpdateGroupChatChannel method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to update the group chat, the Vendor error occured {ex.Message}");
            }

            return OperationResult.Success(string.Empty, chat);
        }

        public async Task<OperationResult> UpdatePublicGroupChatsToPrivate()
        {
            try
            {
                var channelList = await ChannelResource.ReadAsync(_chatServiceSid);
                foreach(ChannelResource channel in channelList)
                {

                    if (channel.Type == ChannelResource.ChannelTypeEnum.Public)
                    {
                        var channelOptions = new UpdateChannelOptions(_chatServiceSid, channel.ServiceSid)
                        {
                            
                             //T = ChannelResource.ChannelTypeEnum.Private,
                        };
                       // await ChannelResource.UpdateAsync(channelOptions);

                    }
                }
                return OperationResult.Success("All channels updated successfully");

            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.GetAllGroupChatChannelAsync method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to Get the group chat list, the Vendor error occured {ex.Message}");
            }

           // return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> DeleteChatChannelAsync(string chatSid)
        {
            bool isDeleted;
            try
            {
                isDeleted = await ConversationResource.DeleteAsync(_chatServiceSid, chatSid);
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.DeleteChatChannel method for chat Sid {chatSid} exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to delete the chat with Sid {chatSid}. Error: {ex.Message}");
            }

            if (!isDeleted)
            {
                return OperationResult.Failure($"Unable to delete chat with sid {chatSid}. Please try later and if the problem persist contact support");
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> AddMemberToChatChannel(User userInfo, string email, string chatSid)
        {
            var userName = $"{userInfo.FirstName} {userInfo.LastName}";
            var attributes = new ChatMemberAttributes
            {
                UserId = userInfo.Id,
                Name = userName,
                PreviewImage = userInfo.AvatarUrl
            };
            var attributesSerialized = JsonSerializer.Serialize(attributes);

            var createMemberOptions = new CreateParticipantOptions(_chatServiceSid, chatSid)
            {
                RoleSid = _chatUserRoleSid,
                Attributes = attributesSerialized,
                Identity = email
            };

            var identities = new List<string> { email };
            try
            {
                var membersSet = await ParticipantResource.ReadAsync(_chatServiceSid, chatSid);
                var member = membersSet.FirstOrDefault(m => m.Identity == email);

                if (member != null)
                {
                    return OperationResult.Success($"User with email {email} already member of chat {chatSid}", member.Sid);
                }

                ParticipantResource memberResourceCreated = await ParticipantResource.CreateAsync(createMemberOptions);

                try
                {
                    List<string> permissions = new List<string>()
                    {
                        "editAnyMessage",
                        "editAnyMessageAttributes"
                    };
                    //var RoleOptions = new Twilio.Rest.Conversations.V1.UpdateRoleOptions(channel.Sid, permission: Promoter.ListOfOne("editAnyMessage"));
                    var RoleOptions = new Twilio.Rest.Conversations.V1.UpdateRoleOptions(memberResourceCreated.Sid, permissions);
                    await Twilio.Rest.Conversations.V1.RoleResource.UpdateAsync(RoleOptions);
                }
                catch (ApiException ex)
                {
                    _logger.Log(LogLevel.Error, @$"ChatService.CreateGroupChatChannel Permission update method exception occured: {ex.Message} {Environment.NewLine}
                       StackTrace: {ex.StackTrace}");
                }

                _logger.Log(LogLevel.Information, $@"User resource for {email} has ben created. Member resource has been created: {memberResourceCreated.Url}");
                return OperationResult.Success(string.Empty, memberResourceCreated.Sid);
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error,
                        @$"ChatService.AddMemberToChatChannel method exception occured: {ex.Message} {Environment.NewLine} 
                        For User_Id: {userInfo.Id} - ChatUsuerRoleId: {_chatUserRoleSid} - ChatServiceId: {_chatServiceSid} - ChatId: {chatSid}
                        {Environment.NewLine} StackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to add client to the group chat with sid {chatSid}, the Vendor error occured {ex.Message}");
            }
        }

        public async Task<OperationResult> CreatePeerChatChannelAsync()
        {
            var attributes = new ChatAttributes { Type = ChatTypes.PeerChat };

            var channelOptions = new CreateConversationOptions(_chatServiceSid)
            {
                // Type = ConversationResource.ChannelTypeEnum.Private,
                Attributes = JsonSerializer.Serialize(attributes)
            };

            ConversationResource channel;
            try
            {
                channel = await ConversationResource.CreateAsync(channelOptions);
                _logger.Log(LogLevel.Error, $"ChatService.CreatePeerChatChannelAsync created channel with Sid {channel.Sid}");
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.CreatePeerChatChannelAsync method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to create a chat, the Vendor error occured {ex.Message}");
            }

            return OperationResult.Success(string.Empty, channel.Sid);
        }

        public async Task<OperationResult> DeleteChatMemberAsync(string chatSid, string userEmail)
        {
            var identities = new List<string> { userEmail };

            bool isDeleted;
            try
            {
                var membersSet = await ParticipantResource.ReadAsync(_chatServiceSid, chatSid);
                var member = membersSet.FirstOrDefault(m => m.Identity == userEmail);

                if (!membersSet.Any())
                {
                    return OperationResult.Failure($"Unable to find user with email {userEmail} as member if chat with id {chatSid}");
                }

                var memberSid = membersSet.First().Sid;

                isDeleted = await ParticipantResource.DeleteAsync(_chatServiceSid, chatSid, memberSid);
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.DeleteChatMemberAsync method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to create a chat, the Vendor error occured {ex.Message}");
            }

            return new OperationResult(isDeleted, string.Empty);
        }

        public async Task<OperationResult> ChangeChatFavoriteState(string chatSid, string userEmail, bool isChatFavorite)
        {
            try
            {
                var resourceSet = await ParticipantResource.ReadAsync(_chatServiceSid, chatSid);
                var currentMember = resourceSet.First(m => m.Identity == userEmail);

                var memberAttributes = JsonSerializer.Deserialize<ChatMemberAttributes>(currentMember.Attributes);

                memberAttributes.IsFavorite = isChatFavorite;

                var memberOptions = new UpdateParticipantOptions(_chatServiceSid, chatSid, currentMember.Sid)
                {
                    Attributes = JsonSerializer.Serialize(memberAttributes)
                };

                await ParticipantResource.UpdateAsync(memberOptions);
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.SetChatAsRegular method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to set chat as regular {ex.Message}");
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> GetChatTypeAsync(string chatSid)
        {
            ChatTypes chatType;
            try
            {
                var channelResource = await ConversationResource.FetchAsync(_chatServiceSid, chatSid);
                var attributes = JsonSerializer.Deserialize<ChatAttributes>(channelResource.Attributes);
                chatType = attributes.Type;
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.GetChatTypeAsync method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to read chat details, the Vendor error occured {ex.Message}");
            }

            return OperationResult.Success(string.Empty, chatType);
        }

        public OperationResult GetChatMembers(string chatSid)
        {
            List<ChatUserReadInfo> chatMemberInfos = new List<ChatUserReadInfo>();
            try
            {
                var members = ParticipantResource.Read(_chatServiceSid, chatSid, limit: int.MaxValue);
                foreach (var memberResource in members)
                {
                    chatMemberInfos.Add(new ChatUserReadInfo
                    {
                        Email = memberResource.Identity,
                        LastReadMessageIndex = memberResource.LastReadMessageIndex,
                        LastReadMessageTimeUtc = Convert.ToDateTime(memberResource.LastReadTimestamp)
                    });
                }
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.GetChatMembersAsync method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");

                return OperationResult.Failure($"Unable to read chat members, the Vendor error occured {ex.Message}");
            }

            return OperationResult.Success(string.Empty, chatMemberInfos);
        }

        public async Task<OperationResult> GetExistingChatSidByUniueName(string chatSid)
        {
            try
            {
                var channelResource = await ConversationResource.FetchAsync(_chatServiceSid, chatSid);
                if (!string.IsNullOrEmpty(channelResource.Sid))
                {
                    return OperationResult.Success(string.Empty, channelResource?.Sid);
                }
            }
            catch (ApiException ex)
            {
                _logger.Log(LogLevel.Error, $"ChatService.GetExistingChatSidByUniueName method exception occured: {ex.Message}     \r\nStackTrace: {ex.StackTrace}");
                return OperationResult.Failure($"Unable to get chat sid, the Vendor error occured {ex.Message}");
            }
            return OperationResult.Failure("Unable to get chat sid");
        }
    }
}
