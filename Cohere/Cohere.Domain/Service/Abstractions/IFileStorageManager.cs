using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IFileStorageManager
    {
        Task<OperationResult> UploadFileToStorageAsync(Stream fileStream, string bucketName, string fileKey, string contentType);

        Task<OperationResult> DownloadFileFromStorageAsync(string bucketName, string fileKey);

        OperationResult GetPreSignUrlAsync(string bucketName, string fileKey, string fileName);

        Task<OperationResult> DeleteFileFromPublicStorageAsync(string fileKey);

        Task<OperationResult> DeleteFileFromPublicStorageByUrlAsync(string fileUrl);

        Task<OperationResult> DeleteFileFromNonPublicStorageAsync(string fileKey);

        Task<OperationResult> DeleteFileFromNonPublicStorageByUrlAsync(string fileUrl);

        Task<OperationResult> DeleteFilesFromNonPublicStorageAsync(IEnumerable<string> fileKey);

        Task<OperationResult> UploadObjectAsync(Stream fileStream, string bucketName, string fileKey, string contentType, int partNumber, bool isLastPart, string uploadId, string prevERags);
    }
}
