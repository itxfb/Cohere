using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Service.Abstractions;
using Microsoft.Extensions.Localization;
using ResourceLibrary;

namespace Cohere.Domain.Service
{
    public class FileStorageManager : IFileStorageManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly string _appPublicBucketName;
        private readonly string _appNonPublicBucketName;

        public FileStorageManager(
            IAmazonS3 s3Client,
            IStringLocalizer<SharedResource> localizer,
            string appPublicBucketName,
            string appNonPublicBucketName)
        {
            _s3Client = s3Client;
            _localizer = localizer;
            _appPublicBucketName = appPublicBucketName;
            _appNonPublicBucketName = appNonPublicBucketName;
        }

        public async Task<OperationResult> UploadObjectAsync(Stream fileStream, string bucketName, string fileKey, string contentType, int partNumber, bool isLastPart,
            string uploadId, string prevETags)
        {
            if (partNumber == 1)
            {
                // Setup information required to initiate the multipart upload.
                InitiateMultipartUploadRequest initiateRequest = new InitiateMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = fileKey
                };

                // Initiate the upload.
                InitiateMultipartUploadResponse initResponse =
                    await _s3Client.InitiateMultipartUploadAsync(initiateRequest);
                uploadId = initResponse.UploadId;
            }

            string eTags = "";
            try
            {
                // Retreiving Previous ETags
                var eTagsList = new List<PartETag>();
                if (!string.IsNullOrEmpty(prevETags))
                {
                    eTagsList = SetAllETags(prevETags);
                }

                UploadPartResponse uploadResponse = null;
                UploadPartRequest uploadRequest = null;
                using (var memoryStream = new MemoryStream())
                {
                    await fileStream.CopyToAsync(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    uploadRequest = new UploadPartRequest
                    {
                        BucketName = bucketName,
                        Key = fileKey,
                        UploadId = uploadId,
                        PartNumber = partNumber,
                        PartSize = memoryStream.Length,
                        InputStream = memoryStream,
                        IsLastPart = isLastPart
                    };

                    // Track upload progress.
                    uploadRequest.StreamTransferProgress +=
                        new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                    // Upload a part and add the response to our list.
                    uploadResponse = await _s3Client.UploadPartAsync(uploadRequest);
                }

                if (isLastPart)
                {
                    eTagsList.Add(new PartETag
                    {
                        PartNumber = partNumber,
                        ETag = uploadResponse.ETag
                    });

                    // Setup to complete the upload.
                    CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = fileKey,
                        UploadId = uploadId,
                        PartETags = eTagsList
                    };

                    // Complete the upload.
                    CompleteMultipartUploadResponse completeUploadResponse =
                        await _s3Client.CompleteMultipartUploadAsync(completeRequest);
                }
                else
                {
                    eTagsList.Add(new PartETag
                    {
                        PartNumber = partNumber,
                        ETag = uploadResponse.ETag
                    });

                    //Set the uploadId and eTags with the response
                    eTags = GetAllETags(eTagsList);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("An AmazonS3Exception was thrown: { 0}", exception.Message);

                // Abort the upload.
                AbortMultipartUploadRequest abortMPURequest = new AbortMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = fileKey,
                    UploadId = uploadId
                };
                await _s3Client.AbortMultipartUploadAsync(abortMPURequest);
            }

            FileDetails fileDetails = new FileDetails()
            {
                UploadId = uploadId,
                PrevETags = eTags
            };

            return OperationResult.Success("", fileDetails);
        }

        private List<PartETag> SetAllETags(string prevETags)
        {
            var partETags = new List<PartETag>();
            var splittedPrevETags = prevETags.Split(',');

            for (int i = 0; i < splittedPrevETags.Length; i++)
            {
                partETags.Add(new PartETag
                {
                    PartNumber = Int32.Parse(splittedPrevETags[i]),
                    ETag = splittedPrevETags[i + 1]
                });

                i = i + 1;
            }

            return partETags;
        }

        private string GetAllETags(List<PartETag> newETags)
        {
            var newPartETags = "";
            var isNotFirstTag = false;

            foreach (var eTag in newETags)
            {
                newPartETags += ((isNotFirstTag) ? "," : "") + (eTag.PartNumber.ToString() + ',' + eTag.ETag);
                isNotFirstTag = true;
            }

            return newPartETags;
        }

        public static void UploadPartProgressEventCallback(object sender, StreamTransferProgressArgs e)
        {
            // Process event. 
            Console.WriteLine("{0}/{1}", e.TransferredBytes, e.TotalBytes);
        }

        public async Task<OperationResult> UploadFileToStorageAsync(Stream fileStream, string bucketName, string fileKey, string contentType)
        {
            try
            {
                var fileTransferUtilityRequest = new TransferUtilityUploadRequest
                {
                    InputStream = fileStream,
                    AutoCloseStream = false,
                    BucketName = bucketName,
                    Key = fileKey,
                    StorageClass = S3StorageClass.StandardInfrequentAccess,
                    PartSize = 26214400 // 25 MB.
                };
                fileTransferUtilityRequest.Headers.ContentType = contentType;
                var transferUtility = new TransferUtility(_s3Client);

                await transferUtility.UploadAsync(fileTransferUtilityRequest);

                var fileUrl = $"https://{bucketName}.s3.amazonaws.com/{fileKey}";

                return new OperationResult(true, "File has been uploaded", fileUrl);
            }
            catch (AmazonS3Exception e)
            {
                return new OperationResult(false, $"{_localizer["Error during upload"]}: {e.Message}");
            }
            catch (Exception e)
            {
                return new OperationResult(false, $"{_localizer["Unknown error"]}: {e.Message}");
            }
        }

        public async Task<OperationResult> DownloadFileFromStorageAsync(string bucketName, string fileKey)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileKey
                };
                using GetObjectResponse response = await _s3Client.GetObjectAsync(request);
                using var fileStream = response.ResponseStream;
                var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                return OperationResult.Success(string.Empty, memoryStream);
            }
            catch (AmazonS3Exception e)
            {
                return OperationResult.Failure($"Error during download {e.Message}");
            }
            catch (Exception e)
            {
                return OperationResult.Failure($"{_localizer["Unknown error"]}: {e.Message}");
            }
        }

        public OperationResult GetPreSignUrlAsync(string bucketName, string fileKey, string fileName)
        {
            try
            {
                GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = fileKey,
                    Expires = DateTime.UtcNow.AddMinutes(5)
                };
                request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename={fileName}";
                request.ResponseHeaderOverrides.ContentType = "application/octet-stream";

                // Get path for request
                var path = _s3Client.GetPreSignedURL(request);

                return OperationResult.Success(string.Empty, path);
            }
            catch (AmazonS3Exception e)
            {
                return OperationResult.Failure($"Error during download {e.Message}");
            }
            catch (Exception e)
            {
                return OperationResult.Failure($"{_localizer["Unknown error"]}: {e.Message}");
            }
        }

        public async Task<OperationResult> DeleteFileFromPublicStorageAsync(string fileKey)
        {
            return await DeleteFileFromStorage(_appPublicBucketName, fileKey);
        }

        public async Task<OperationResult> DeleteFileFromPublicStorageByUrlAsync(string fileUrl)
        {
            var fileKey = fileUrl.Replace($"https://{_appPublicBucketName}.s3.amazonaws.com/", string.Empty);

            return await DeleteFileFromPublicStorageAsync(fileKey);
        }

        public async Task<OperationResult> DeleteFileFromNonPublicStorageAsync(string fileKey)
        {
            return await DeleteFileFromStorage(_appNonPublicBucketName, fileKey);
        }

        public async Task<OperationResult> DeleteFilesFromNonPublicStorageAsync(IEnumerable<string> fileKey)
        {
            return await DeleteFilesFromStorage(_appNonPublicBucketName, fileKey);
        }

        public async Task<OperationResult> DeleteFileFromNonPublicStorageByUrlAsync(string fileUrl)
        {
            var fileKey = fileUrl.Replace($"https://{_appNonPublicBucketName}.s3.amazonaws.com/", string.Empty);

            return await DeleteFileFromPublicStorageAsync(fileKey);
        }

        /// <summary>
        /// Deletes file for bucket by the key with null version
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <param name="fileKey">The file key.</param>
        /// <returns></returns>
        private async Task<OperationResult> DeleteFilesFromStorage(string bucketName, IEnumerable<string> fileKey)
        {
            try
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = bucketName,
                    Objects = fileKey.Select(f => new KeyVersion {Key = f, VersionId = null}).ToList()
                };

                await _s3Client.DeleteObjectsAsync(deleteRequest);

                return OperationResult.Success(_localizer["Object deleted"]);
            }
            catch (AmazonS3Exception e)
            {
                return OperationResult.Failure($"{_localizer["Error during deleting"]}: {e.Message}");
            }
            catch (Exception e)
            {
                return OperationResult.Failure($"{_localizer["Unknown error"]}: {e.Message}");
            }
        }

        private async Task<OperationResult> DeleteFileFromStorage(string bucketName, string fileKey)
        {
            try
            {
                await _s3Client.DeleteObjectAsync(bucketName, fileKey);

                return OperationResult.Success(_localizer["Object deleted"]);
            }
            catch (AmazonS3Exception e)
            {
                return OperationResult.Failure($"{_localizer["Error during deleting"]}: {e.Message}");
            }
            catch (Exception e)
            {
                return OperationResult.Failure($"{_localizer["Unknown error"]}: {e.Message}");
            }
        }
    }
}