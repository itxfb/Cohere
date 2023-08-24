using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Video;
using System;
using System.Threading.Tasks;
using Cohere.Domain.Models.ContributionViewModels;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IVideoService
    {
        Task<OperationResult> GetClientTokenAsync(GetVideoTokenViewModel model, string requesterAccountId);

        Task<OperationResult> CreateRoom(GetVideoTokenViewModel viewModel, string cohealerAccountId);

        Task<OperationResult> DeleteRoom(DeleteRoomInfoViewModel viewModel, string requesterAccountId);

        Task<OperationResult> HandleRoomDeletionVendorConfirmation(string contributionId, string classId);

        Task NotifyVideoRetrievalService(string compositionId, string roomSid, DateTime timeOfRecording);

        Task<string> GetPresignedUrl(string accountId, string roomId, string contributionId, bool allowAnonymous = false);

        Task<OperationResult> GetRoomStatus(string accountId, string contributionId, string classId);

        Task<OperationResult> GetPresignedUrlForRecordedSession(string accountId, string contributionId, string sessionId, string sessionTimeId);

        string GetVideoUrl(string videoKey);
    }
}
