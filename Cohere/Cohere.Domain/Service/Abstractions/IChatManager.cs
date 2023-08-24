using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IChatManager
    {
        string GetToken(string identityName);

        Task<OperationResult> CreateContributionChatChannelAsync(GroupChat chat, string contributionId);

        Task<OperationResult> UpdateGroupChatChannelAsync(GroupChat chat, string contributionId);
        Task<OperationResult> UpdatePublicGroupChatsToPrivate();

        Task<OperationResult> DeleteChatChannelAsync(string chatSid);

        Task<OperationResult> AddMemberToChatChannel(User userInfo, string email, string chatSid);

        Task<OperationResult> CreatePeerChatChannelAsync();

        Task<OperationResult> DeleteChatMemberAsync(string chatSid, string userEmail);

        Task<OperationResult> ChangeChatFavoriteState(string chatSid, string userEmail, bool isChatFavorite);

        Task<OperationResult> GetChatTypeAsync(string chatSid);

        OperationResult GetChatMembers(string chatSid);

        Task<OperationResult> GetExistingChatSidByUniueName(string chatSid);
    }
}
