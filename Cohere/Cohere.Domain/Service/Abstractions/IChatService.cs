using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Chat.WebhookHandling;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IChatService
    {
        Task<string> GetChatTokenForClientAsync(string accountId);

        Task<OperationResult> AddClientToContributionRelatedChat(string userId, ContributionBase contribution);

        Task<OperationResult<GroupChat>> CreateChatForContribution(ContributionBase contribution);

        Task<OperationResult> UpdateChatForContribution(ContributionBase contribution);

        Task<OperationResult> DeleteChatForContribution(string chatSid);

        Task<OperationResult> CreatePeerChat(string requestorAccountId, string peerUserId, bool IsOpportunity = false);
        Task<OperationResult> LeaveContributionChat(string contributionId, string requestorAccountId);

        Task<OperationResult> LeavePeerChat(string chatSid, string requestorAccountId);

        Task<OperationResult> ChangeChatFavoriteState(string requestorAccountId, string chatSid, bool isChatFavorite);
        Task<OperationResult> PinChat(string chatSid, bool isPinned);

        Task<List<string>> GetChatSidsByType(string requestorAccountId, string chatType, bool isCohealer);

        Task<OperationResult> AddUserToChat(string userId, string chatSid);

        Task<OperationResult> RemoveUserFromChat(string userId, string chatSid);

        OperationResult HandleChatEvent(ChatEventModel chatEvent);

        Task<OperationResult<string>> GetExistingChatSidByUniueName(ContributionBase contribution);
        Task<OperationResult> CreateCustomerSupportChat(string requestorAccountId);
    }
}
