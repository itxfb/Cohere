using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Chat;
using Cohere.Domain.Models.Chat.WebhookHandling;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Chat;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Entity.Entities.Chat;

namespace Cohere.Domain.Service
{
    public class ChatService : IChatService
    {
        private readonly ILogger<ChatService> _logger;
        private readonly IChatManager _chatManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IContributionRootService _contributionRootService;
        private readonly ICommonService _commonService;


        public ChatService(
            ILogger<ChatService> logger,
            IChatManager chatManager,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IContributionRootService contributionRootService,
            ICommonService commonService)
        {
            _logger = logger;
            _chatManager = chatManager;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _contributionRootService = contributionRootService;
            _commonService = commonService;
        }

        public async Task<string> GetChatTokenForClientAsync(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);
            return _chatManager.GetToken(account.Email);
        }

        public async Task<OperationResult> AddClientToContributionRelatedChat(string userId, ContributionBase contribution)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            if (user == null)
            {
                return OperationResult.Failure($"Client not added to chat channel, unable to find user info for user with id {userId}");
            }

            if (contribution is SessionBasedContribution)
            {
                var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == user.AccountId);
                if (account == null)
                {
                    return OperationResult.Failure($"Client not added to chat channel, unable to find account info for account with id {user.AccountId}");
                }

                var addMemberResult = await _chatManager.AddMemberToChatChannel(user, account.Email, contribution.Chat.Sid);

                if (!addMemberResult.Succeeded)
                {
                    return addMemberResult;
                }
            }

            var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            if(authorUser == null)
            {
                return OperationResult.Failure($"Client not added to chat channel, unable to find user (author) info for user with id {contribution.UserId}");
            }
            var createPeerChatResult = await CreatePeerChat(authorUser.AccountId, userId);

            if (!createPeerChatResult.Succeeded)
            {
                return createPeerChatResult;
            }

            var chatCreated = (PeerChat)createPeerChatResult.Payload;

            if (contribution.Chat == null)
            {
                contribution.Chat = new GroupChat();
            }

            var isNotExistsInContributionPeerChatList = contribution.Chat.CohealerPeerChatSids.TryAdd(userId, chatCreated.Sid);

            if (isNotExistsInContributionPeerChatList)
            {
                try
                {
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                }
                catch (Exception ex)
                {
                    var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chatCreated.Sid);

                    var messageForDbUpdateError =
                        $"Unable to add chat info to 1:1 contribution in database with Id {contribution.Id}. Error: {ex.Message}. Just created peer chat with Sid {chatCreated.Sid} has been deleted: {chatDeletionResult.Succeeded}. Chat deletion result message {chatDeletionResult.Message}";
                    _logger.Log(LogLevel.Error, messageForDbUpdateError);
                    return OperationResult.Failure(messageForDbUpdateError);
                }
            }

            return createPeerChatResult;
        }

        public async Task<OperationResult<GroupChat>> CreateChatForContribution(ContributionBase contribution)
        {
            var chat = new GroupChat
            {
                FriendlyName = contribution.Title,
                PreviewImageUrl = contribution.PreviewContentUrls.FirstOrDefault()
            };

            var chatCreationResult = await _chatManager.CreateContributionChatChannelAsync(chat, contribution.Id);

            if (chatCreationResult.Failed)
            {
                return OperationResult<GroupChat>.Failure(chatCreationResult.Message);
            }

            var chatFromResult = (GroupChat)chatCreationResult.Payload;
            var authorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            if(authorUser == null)
            {
                return OperationResult<GroupChat>.Failure($"Could not find user ith id {contribution.UserId}");
            }
            var authorAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == authorUser.AccountId);

            if (authorAccount == null)
            {
                return OperationResult<GroupChat>.Failure($"Could not find account ith id {authorUser.AccountId}");
            }
            var addAuthorResult =
                await _chatManager.AddMemberToChatChannel(authorUser, authorAccount.Email, chatFromResult.Sid);

            if (addAuthorResult.Failed)
            {
                var chatCleanUpResult = await _chatManager.DeleteChatChannelAsync(chatFromResult.Sid);

                _logger.Log(LogLevel.Error,
                    @$"Unable to add contribution author to just created chat with Sid {chatFromResult.Sid}.
                Error occured {addAuthorResult.Message}.
                Chat has been deleted: {chatCleanUpResult.Succeeded}. Chat deletion result message {chatCleanUpResult.Message}");

                return OperationResult<GroupChat>.Failure(addAuthorResult.Message);
            }

            return OperationResult<GroupChat>.Success((GroupChat)chatCreationResult.Payload);
        }

        public async Task<OperationResult> UpdateChatForContribution(ContributionBase contribution)
        {
            var chat = new GroupChat
            {
                Sid = contribution.Chat.Sid,
                FriendlyName = contribution.Title,
                PreviewImageUrl = contribution.PreviewContentUrls.FirstOrDefault()
            };

            return await _chatManager.UpdateGroupChatChannelAsync(chat, contribution.Id);
        }

        public async Task<OperationResult> DeleteChatForContribution(string chatSid)
        {
            var chatCreationResult = await _chatManager.DeleteChatChannelAsync(chatSid);

            if (!chatCreationResult.Succeeded)
            {
                return chatCreationResult;
            }

            return OperationResult.Success(string.Empty, chatCreationResult.Payload);
        }

        public async Task<OperationResult> CreatePeerChat(string requestorAccountId, string peerUserId, bool IsOpportunity = false)
        {
            var requesterUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (requesterUser.Id == peerUserId)
            {
                return OperationResult.Failure("Not allowed to create peer chat with yourself");
            }

            var requesterAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);
            var peerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == peerUserId);

            var peerChatsWithRequester = await _unitOfWork.GetRepositoryAsync<PeerChat>()
                .Get(c => c.Participants.Any(p => p.UserId == requesterUser.Id && !p.IsLeft));
            var peerChatExisted = peerChatsWithRequester
                .FirstOrDefault(c => c.Participants.All(p => (p.UserId == requesterUser.Id && !p.IsLeft) ||
                                                             (p.UserId == peerUserId && !p.IsLeft)));

            if (peerChatExisted != null)
            {
                if (peerChatExisted.IsOpportunity && !IsOpportunity)
                {
                    peerChatExisted.IsOpportunity = false;
                    await _unitOfWork.GetRepositoryAsync<PeerChat>().Update(peerChatExisted.Id, peerChatExisted);
                }
                _logger.Log(LogLevel.Information, $"Peer chat between users with id {requesterUser.Id} and {peerUserId} already exists. The chat Id: {peerChatExisted.Id}, chat Sid: {peerChatExisted.Sid}");
                return OperationResult.Success(string.Empty, peerChatExisted);
            }

            if (peerUser == null)
            {
                return OperationResult.Failure($"Chat is not created. Unable to find peer with Id {peerUserId}");
            }

            var peerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == peerUser.AccountId);

            var chatCreationResult = await _chatManager.CreatePeerChatChannelAsync();

            if (!chatCreationResult.Succeeded)
            {
                return chatCreationResult;
            }

            var chat = new PeerChat
            {
                Sid = (string)chatCreationResult.Payload
            };

            var participant1MemberCreationResult = await _chatManager.AddMemberToChatChannel(requesterUser, requesterAccount.Email, chat.Sid);
            if (!participant1MemberCreationResult.Succeeded)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);

                var resultMessage =
                    @$"Unable to add requestor to just created peer chat, error: {participant1MemberCreationResult.Message}.
                    Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";

                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }

            var participant1 = new ChatParticipant
            {
                UserId = requesterUser.Id,
                MemberSid = (string)participant1MemberCreationResult.Payload
            };
            chat.Participants.Add(participant1);

            var participant2MemberCreationResult = await _chatManager.AddMemberToChatChannel(peerUser, peerAccount.Email, chat.Sid);
            if (!participant2MemberCreationResult.Succeeded)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);
                var resultMessage =
                    @$"Unable to add peer user to just created peer chat, error: {participant2MemberCreationResult.Message}.
                Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";

                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }

            var participant2 = new ChatParticipant
            {
                UserId = peerUser.Id,
                MemberSid = (string)participant2MemberCreationResult.Payload
            };
            chat.Participants.Add(participant2);

            PeerChat chatInserted;
            try
            {
                if (IsOpportunity)
                {
                    chat.IsOpportunity = true;
                }
                chatInserted = await _unitOfWork.GetRepositoryAsync<PeerChat>().Insert(chat);
            }
            catch (Exception ex)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);
                var resultMessage = @$"Unable to add peer chat information to database, error: {ex.Message}.
                Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";

                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }

            return OperationResult.Success(string.Empty, chatInserted);
        }

        public async Task<OperationResult> LeaveContributionChat(string contributionId, string requestorAccountId)
        {
            var requesterAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);

            var contribution = await _contributionRootService.GetOne(contributionId);
            if (contribution == null)
            {
                return OperationResult.Failure("Unable to find contribution to leave chat associated with it");
            }

            var leaveChatResult = await _chatManager.DeleteChatMemberAsync(contribution.Chat.Sid, requesterAccount.Email);

            if (!leaveChatResult.Succeeded)
            {
                return leaveChatResult;
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> LeavePeerChat(string chatSid, string requestorAccountId)
        {
            var requestorUser =
                await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);
            var requestorAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);

            var chatInDb = await _unitOfWork.GetRepositoryAsync<PeerChat>().GetOne(c => c.Sid == chatSid);
            if (chatInDb == null)
            {
                return OperationResult.Failure("Unable to find chat information in database to leave");
            }

            var participant = chatInDb.Participants.FirstOrDefault(p => p.UserId == requestorUser.Id);
            if (participant == null)
            {
                return OperationResult.Failure(@"Unable to delete the member from chat. User is not a member");
            }

            participant.IsLeft = true;
            participant.DateTimeLeft = DateTime.UtcNow;

            if (chatInDb.Participants.All(p => p.IsLeft))
            {
                var deleteChannelResult = await _chatManager.DeleteChatChannelAsync(chatInDb.Sid);

                if (!deleteChannelResult.Succeeded)
                {
                    return deleteChannelResult;
                }
            }
            else
            {
                var leaveChatResult = await _chatManager.DeleteChatMemberAsync(chatInDb.Sid, requestorAccount.Email);

                if (!leaveChatResult.Succeeded)
                {
                    return leaveChatResult;
                }
            }

            PeerChat updatedChatInDb;
            try
            {
                updatedChatInDb = await _unitOfWork.GetRepositoryAsync<PeerChat>().Update(chatInDb.Id, chatInDb);
            }
            catch (Exception ex)
            {
                var resultMessage = $"{requestorAccount.Email} left chat with Sid {chatSid}.BUT unable to delete participant from peer chat in database, error: {ex.Message}. Please contact support to delete chat manually";

                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }

            return OperationResult.Success(string.Empty, updatedChatInDb);
        }

        public async Task<OperationResult> ChangeChatFavoriteState(string requestorAccountId, string chatSid, bool isChatFavorite)
        {
            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(u => u.Id == requestorAccountId);

            var setAsFavoriteResult = await _chatManager.ChangeChatFavoriteState(chatSid, requestorAccount.Email, isChatFavorite);

            return setAsFavoriteResult;
        }
        public async Task<OperationResult> PinChat( string chatSid, bool isPinned)
        {
            var chattobePinned = await _unitOfWork.GetRepositoryAsync<ChatConversation>().GetOne(m=>m.ChatSid == chatSid);
            if (chattobePinned != null)
            {
                chattobePinned.IsPinned = isPinned;
            }
            var pinnedChat = await _unitOfWork.GetRepositoryAsync<ChatConversation>().Update(chattobePinned.Id, chattobePinned);
            return OperationResult.Success(string.Empty, pinnedChat.ChatSid);
        }

        public async Task<List<string>> GetChatSidsByType(string requestorAccountId, string chatType, bool isCohealer)
        {
            if (!Enum.IsDefined(typeof(UserRequestChatTypes), chatType))
            {
                return null;
            }

            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            switch (Enum.Parse<UserRequestChatTypes>(chatType))
            {
                case (UserRequestChatTypes.GroupDiscussion):
                    {
                        var chatSidsList = new List<string>();

                        // For requestor as a Client
                        var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                            .Get(p => p.ClientId == requestorUser.Id && p.ContributionType != nameof(ContributionOneToOne));

                        if (purchases.Any())
                        {
                            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
                            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
                            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));

                            var successPurchases = purchaseVms.Where(p => p.HasSucceededPayment).ToList();
                            if (successPurchases.Any())
                            {
                                var userContributionIds = successPurchases.Select(p => p.ContributionId);

                                var contributions = await _contributionRootService
                                    .Get(c => userContributionIds.Contains(c.Id));
                                var clientGroupChats = contributions.Select(c => c.Chat).Where(x => x != null);
                                chatSidsList.AddRange(clientGroupChats.Select(c => c.Sid));
                            }
                        }

                        if (!isCohealer)
                        {
                            return chatSidsList;
                        }

                        // For requestor as a Cohealer
                        var requestorContributions = await _contributionRootService
                            .Get(c => c.UserId == requestorUser.Id && c.Status == ContributionStatuses.Approved);

                        var partnerChats = await _contributionRootService.Get(x => x.Partners.Any(y => y.IsAssigned == true && y.UserId == requestorUser.Id));

                        var requestorContributionsList = requestorContributions.ToList();
                        if (requestorContributionsList.Any())
                        {
                            var courseContributions =
                                requestorContributionsList.Where(c => c.Type == nameof(ContributionCourse) || c.Type == nameof(ContributionMembership) || c.Type == nameof(ContributionCommunity)).ToList();
                            if (courseContributions.Any())
                            {
                                var cohealerGroupChats = courseContributions.Select(c => c.Chat).Where(a=> a!=null);
                                chatSidsList.AddRange(cohealerGroupChats.Select(c => c.Sid));
                            }
                        }

                        chatSidsList.AddRange(partnerChats.Select(x => x.Chat.Sid));
                        return chatSidsList;
                    }

                case (UserRequestChatTypes.DirectWithCohealers):
                    {
                        var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == requestorUser.Id);

                        if (!purchases.Any())
                        {
                            return new List<string>();
                        }

                        var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
                        var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
                        purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
                        var successPurchases = purchaseVms.Where(p => p.HasSucceededPayment).ToList();
                        if (!successPurchases.Any())
                        {
                            return new List<string>();
                        }

                        var userContributionIds = successPurchases.Select(p => p.ContributionId);

                        var contributions = await _contributionRootService
                            .Get(c => userContributionIds.Contains(c.Id));

                        var peerChats = contributions
                            .Where(c => c.Chat != null && c.Chat.CohealerPeerChatSids.Any())
                            .Select(c => c.Chat);
                        var withCohealerChatsSids = peerChats
                            .SelectMany(c => c.CohealerPeerChatSids)
                            .Where(chat => chat.Key == requestorUser.Id)
                            .Select(pair => pair.Value)
                            .ToList();

                        var partnerPeerChats = contributions.Where(c => c.Chat?.PartnerChats?.Any() == true)
                            .SelectMany(x => x.Chat.PartnerChats.SelectMany(y => y?.PeerChats))
                            .Where(x => x != null).ToList();

                        if (partnerPeerChats?.Any() == true)
                        {
                            var userPartnerChats = partnerPeerChats
                                .Where(x => x.UserId == requestorUser.Id)
                                .Select(x => x.ChatSid)
                                .ToList();
                            withCohealerChatsSids.AddRange(userPartnerChats);
                        }

                        return withCohealerChatsSids;
                    }

                case (UserRequestChatTypes.DirectWithClients):
                    {
                        var requesterContributions = await _contributionRootService
                            .Get(c => c.UserId == requestorUser.Id && c.Status == ContributionStatuses.Approved);

                        var allContributions = requesterContributions.ToList();

                        var peerChats = allContributions
                            .Where(c => c.Chat != null && c.Chat.CohealerPeerChatSids.Any())
                            .Select(c => c.Chat);

                        var withClientChatsSids = peerChats
                            .SelectMany(c => c.CohealerPeerChatSids)
                            .Select(pair => pair.Value)
                            .ToList();

                        var partnerContributions = await _contributionRootService.Get(x => x.Partners.Any(y => y.UserId == requestorUser.Id && y.IsAssigned == true));
                        var partnerChatsSids = partnerContributions
                            .Where(x => x.Chat != null)
                            .SelectMany(x => x.Chat.PartnerChats.Where(y => y.PartnerUserId == requestorUser.Id))
                            .SelectMany(x => x.PeerChats.Select(y => y.ChatSid));

                        withClientChatsSids.AddRange(partnerChatsSids);

                        return withClientChatsSids;
                    }
                case (UserRequestChatTypes.Opportunities):
                    {
                        var peerChatsWithRequester = await _unitOfWork.GetRepositoryAsync<PeerChat>()
                .Get(c => c.Participants.Any(p => p.UserId == requestorUser.Id && !p.IsLeft) && c.IsOpportunity);
                        return peerChatsWithRequester.Select(x => x.Sid).ToList();
                    }
                default:
                    return null;
            }
        }

        public OperationResult HandleChatEvent(ChatEventModel chatEvent)
        {
            try
            {
                _logger.Log(LogLevel.Information, $"BEFORE enter {Thread.CurrentThread.ManagedThreadId}");

                using (MachineLock.Create(chatEvent.ChannelSid, TimeSpan.FromMilliseconds(Constants.Chat.TimeToWaitTaskCompletedMilliseconds), Constants.Chat.NumberOfRetry))
                {
                    _logger.Log(LogLevel.Information, $"Entering {Thread.CurrentThread.ManagedThreadId}");

                    var chatConversationExisted = _unitOfWork.GetRepositoryAsync<ChatConversation>()
                        .GetOne(c => c.ChatSid == chatEvent.ChannelSid).GetAwaiter().GetResult();

                    var getChatTypeResult = _chatManager.GetChatTypeAsync(chatEvent.ChannelSid).GetAwaiter().GetResult();
                    if (!getChatTypeResult.Succeeded)
                    {
                        return getChatTypeResult;
                    }

                    var chatType = (ChatTypes)getChatTypeResult.Payload;

                    if (chatConversationExisted == null)
                    {
                        var getChatMemberInfosResult = _chatManager.GetChatMembers(chatEvent.ChannelSid);
                        if (!getChatMemberInfosResult.Succeeded)
                        {
                            return getChatMemberInfosResult;
                        }

                        var chatConversationNew = new ChatConversation
                        {
                            ChatSid = chatEvent.ChannelSid,
                            ChatType = chatType,
                            UserReadInfos = (List<ChatUserReadInfo>)getChatMemberInfosResult.Payload
                        };

                        chatConversationExisted = _unitOfWork.GetRepositoryAsync<ChatConversation>().Insert(chatConversationNew).GetAwaiter().GetResult();
                    }

                    if (chatEvent is ChatMessageAddedModel messageAddedModel)
                    {
                        chatConversationExisted.LastMessageAuthorUserId = messageAddedModel.From;
                        chatConversationExisted.LastMessageIndex = messageAddedModel.Index;
                        chatConversationExisted.LastMessageAddedTimeUtc = messageAddedModel.DateCreated;
                    }

                    if (chatEvent is ChatMediaMessageAddedModel mediaMessageAddedModel)
                    {
                        chatConversationExisted.LastMessageAuthorUserId = mediaMessageAddedModel.From;
                        chatConversationExisted.LastMessageIndex = mediaMessageAddedModel.Index;
                        chatConversationExisted.LastMessageAddedTimeUtc = mediaMessageAddedModel.DateCreated;
                    }

                    if (chatEvent is ChatMemberUpdatedModel memberUpdatedModel)
                    {
                        var existedMemberInfo = chatConversationExisted.UserReadInfos.FirstOrDefault(i => i.Email == memberUpdatedModel.Identity);
                        if (existedMemberInfo == null)
                        {
                            chatConversationExisted.UserReadInfos.Add(new ChatUserReadInfo
                            {
                                Email = memberUpdatedModel.Identity,
                                LastReadMessageTimeUtc = DateTime.UtcNow,
                                LastReadMessageIndex = memberUpdatedModel.LastConsumedMessageIndex
                            });
                        }
                        else
                        {
                            var memberUpdatedHasFirstMessageIndex =
                                memberUpdatedModel.LastConsumedMessageIndex.HasValue && !existedMemberInfo.LastReadMessageIndex.HasValue;

                            var memberUpdatedHasNewMessageIndex =
                                memberUpdatedModel.LastConsumedMessageIndex.HasValue && existedMemberInfo.LastReadMessageIndex.HasValue &&
                                memberUpdatedModel.LastConsumedMessageIndex > existedMemberInfo.LastReadMessageIndex;

                            if (memberUpdatedHasFirstMessageIndex || memberUpdatedHasNewMessageIndex)
                            {
                                existedMemberInfo.LastReadMessageIndex = memberUpdatedModel.LastConsumedMessageIndex;
                                existedMemberInfo.LastReadMessageTimeUtc = DateTime.UtcNow;
                                existedMemberInfo.FirstNotificationSentUtc = default;
                                existedMemberInfo.SecondNotificationSentUtc = default;
                            }
                        }
                    }

                    var atLeastOneChatUserDoesntHaveReadMessages = chatConversationExisted.UserReadInfos.Any(i => !i.LastReadMessageIndex.HasValue);

                    var atLeastOneChatUserHasReadMessagesButNotAll = chatConversationExisted.UserReadInfos
                        .Any(i => i.LastReadMessageIndex.HasValue && i.LastReadMessageIndex < chatConversationExisted.LastMessageIndex);

                    chatConversationExisted.HasUnread = chatConversationExisted.LastMessageIndex.HasValue &&
                                                        (atLeastOneChatUserDoesntHaveReadMessages || atLeastOneChatUserHasReadMessagesButNotAll);

                    try
                    {
                        _unitOfWork.GetRepositoryAsync<ChatConversation>().Update(chatConversationExisted.Id, chatConversationExisted).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Chat information is not updated for Sid:{chatConversationExisted.Id}. Database exception occurred {ex.Message}";
                        _logger.Log(LogLevel.Error, errorMessage);
                        return OperationResult.Failure(errorMessage);
                    }

                    _logger.Log(LogLevel.Information, $"Exiting {Thread.CurrentThread.ManagedThreadId}");
                }
            }
            catch (MachineLockTimeoutException)
            {
                _logger.Log(LogLevel.Error, $"Exception for thread: {Thread.CurrentThread.ManagedThreadId}");
                //Ignoring
            }

            _logger.Log(LogLevel.Information, $"Chat with Sid: '{chatEvent.ChannelSid}' is being processed at the moment");

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> AddUserToChat(string userId, string chatSid)
        {
            var userRepository = _unitOfWork.GetRepositoryAsync<User>();
            var accountRepository = _unitOfWork.GetRepositoryAsync<Account>();
            var user = await userRepository.GetOne(x => x.Id == userId);
            var account = await accountRepository.GetOne(x => x.Id == user.AccountId);

            await _chatManager.AddMemberToChatChannel(user, account.Email, chatSid);

            return new OperationResult(true, null);
        }

        public async Task<OperationResult> RemoveUserFromChat(string userId, string chatSid)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.Id == userId);
            if (user == null)
            {
                return new OperationResult(false, "User not found");
            }

            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(x => x.Id == user.AccountId);
            if (account == null)
            {
                return new OperationResult(false, "Account not found");
            }

            return await _chatManager.DeleteChatMemberAsync(chatSid, account.Email);
        }

        public async Task<OperationResult<string>> GetExistingChatSidByUniueName(ContributionBase contribution)
        {
            var getChatSidResult = await _chatManager.GetExistingChatSidByUniueName(contribution?.Id);

            if (getChatSidResult.Failed)
            {
                return OperationResult<string>.Failure(getChatSidResult.Message);
            }
            return OperationResult<string>.Success(getChatSidResult.Payload?.ToString());
        }
        public async Task<OperationResult> CreateCustomerSupportChat(string requestorAccountId)
        {
            var requesterUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);
            var peerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(m => m.Email == "hasiaglaim@gmail.com");
            var requesterAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);
            var peerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == peerAccount.Id);
            var peerChatsWithRequester = await _unitOfWork.GetRepositoryAsync<CustomerSupportChat>()
                .Get(c => c.Participants.Any(p => p.UserId == requesterUser.Id && !p.IsLeft));
            var peerChatExisted = peerChatsWithRequester
                .FirstOrDefault(c => c.Participants.All(p => (p.UserId == requesterUser.Id && !p.IsLeft) ||
                                                             (p.UserId == peerUser.Id && !p.IsLeft)));
            if (peerChatExisted != null)
            {
                _logger.Log(LogLevel.Information, $"Customer support chat between users with id {requesterUser.Id} and {peerUser.Id} already exists. The chat Id: {peerChatExisted.Id}, chat Sid: {peerChatExisted.Sid}");
                return OperationResult.Success(string.Empty, peerChatExisted);
            }
            if (peerUser == null)
            {
                return OperationResult.Failure($"Chat is not created. Unable to find peer with Id {peerUser.Id}");
            }
            var chatCreationResult = await _chatManager.CreatePeerChatChannelAsync();
            if (!chatCreationResult.Succeeded)
            {
                return chatCreationResult;
            }
            var chat = new CustomerSupportChat
            {
                Sid = (string)chatCreationResult.Payload
            };
            var participant1MemberCreationResult = await _chatManager.AddMemberToChatChannel(requesterUser, requesterAccount.Email, chat.Sid);
            if (!participant1MemberCreationResult.Succeeded)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);
                var resultMessage =
                    @$"Unable to add requestor to just created peer chat, error: {participant1MemberCreationResult.Message}.
                    Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";
                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }
            var participant1 = new ChatParticipant
            {
                UserId = requesterUser.Id,
                MemberSid = (string)participant1MemberCreationResult.Payload
            };
            chat.Participants.Add(participant1);
            var participant2MemberCreationResult = await _chatManager.AddMemberToChatChannel(peerUser, peerAccount.Email, chat.Sid);
            if (!participant2MemberCreationResult.Succeeded)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);
                var resultMessage =
                    @$"Unable to add peer user to just created peer chat, error: {participant2MemberCreationResult.Message}.
                Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";
                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }
            var participant2 = new ChatParticipant
            {
                UserId = peerUser.Id,
                MemberSid = (string)participant2MemberCreationResult.Payload
            };
            chat.Participants.Add(participant2);
            CustomerSupportChat chatInserted;
            try
            {
                chatInserted = await _unitOfWork.GetRepositoryAsync<CustomerSupportChat>().Insert(chat);
            }
            catch (Exception ex)
            {
                var chatDeletionResult = await _chatManager.DeleteChatChannelAsync(chat.Sid);
                var resultMessage = @$"Unable to add peer chat information to database, error: {ex.Message}.
                Created chat has been deleted, deletion successful: {chatDeletionResult.Succeeded}, message: {chatDeletionResult.Message}";
                _logger.Log(LogLevel.Error, resultMessage);
                return OperationResult.Failure(resultMessage);
            }
            return OperationResult.Success(string.Empty, chatInserted);
        }
    }
}
