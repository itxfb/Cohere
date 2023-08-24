using System.IO;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Content;
using Cohere.Domain.Models.ModelsAuxiliary;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContentService
    {
        Task<OperationResult> UploadAttachmentOnContribCreateAsync(FileDetails fileDetails, string contributionId = "", string duration = "");

        Task<OperationResult> UploadRecordedAttachmentOnContribCreateAsync(
            FileDetails fileDetails,
            int partNumber,
            bool isLastPart,
            string documentId,
            string contributionId,
            string duration);

        Task<OperationResult> UploadAttachmentForExistedContribAsync(FileDetails fileDetails, AttachmentWithFileViewModel attachmentVm, int partNumber, bool isLastPart, string documentId);

        Task<OperationResult> DownloadAttachmentAsync(GetAttachmentViewModel attachmentVm, string requestorAccountId);

        Task<OperationResult> DeleteAttachmentAsync(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId);

        Task<OperationResult> UploadAvatarAsync(FileDetails fileDetails);
        Task<OperationResult> DownloadAttachmentSelfPacedAsync(GetAttachmentViewModel attachmentVm, string requestorAccountId);
        Task<OperationResult> UploadPublicFileAsync(FileDetails fileDetails);
        Task<OperationResult> DeleteAttachmentSelfPacedAsync(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId, bool isVideo);
        Task<OperationResult> DeletePublicImageAsync(string imageUrl);

        Task<OperationResult> UploadTestimonialAvatarAsync(FileDetails fileDetails, string contributionId, string previousUrl);

        Task<OperationResult> DeleteSessionTimeRecording(string contributionId, string sessionTimeId, string roomId, string compositionFileName);
        Task<OperationResult> DeleteSelfpacedVideoOnContributionCreation(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId);
        Task<OperationResult> UploadSessionTimeRecord(FileDetails fileDetails, AttachmentWithFileViewModel model, string sessionTimeId, string roomId, int partNumber, bool isLastPart, string documentId, int duration, string replyLink);
        Task<Stream> GetFileFromS3Async(string fileLink);
        string GetFileKey(string fileLink);
        Task<OperationResult> RemoveCustomLogo(string accountId, string imageUrl);
    }
}
