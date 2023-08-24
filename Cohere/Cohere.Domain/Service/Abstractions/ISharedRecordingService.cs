using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Video;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface ISharedRecordingService
    {
        Task<OperationResult> InsertInfoToShareRecording(string contributionId, string sessionTimeId, string accountId);
        Task<OperationResult> ChangePassCodeStatus(string contributionId, string sessionTimeId, string accountId, bool isPassCodeEnabled); 
        Task<OperationResult<List<RecordingInfo>>> GetSharedRecordingsInfo(string contributionId, string sessionTimeId, string passCode = null);
        Task<OperationResult<string>> GetSharedRecordingPresignedUrl(string contributionId,string sessionTimeId, string roomId, string passCode = null);
    }
}
