using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models;

namespace Cohere.Domain.Service
{
    public interface IRecordingService
    {
        Task<OperationResult> GetCurrentRoomStatus(RecordingRequestModel request, string accountId);

        Task<OperationResult> ToggleRecording(RecordingRequestModel request, string accountId, bool renewRequest);
    }
}