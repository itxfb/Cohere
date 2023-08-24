using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Content;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Localization;
using ResourceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;

namespace Cohere.Domain.Service
{
    public class ContentService : IContentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageManager _fileService;
        private readonly IContributionService _contributionService;
        private readonly IContributionRootService _contributionRootService;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly INotificationService _notificationService;
        private readonly S3Settings _s3SettingsOptions;
        private readonly StoragePathTemplatesSettings _storagePathTemplatesSettings;
        private readonly ILogger<ContentService> _logger;
        private readonly IAmazonS3 _amazonS3;
        private readonly ClientUrlsSettings _urlSettings;

        private static Regex fileKeyRegex = new Regex("(?<=s3.amazonaws.com/)(.*\\.*)");

        public ContentService(
			IUnitOfWork unitOfWork,
            IFileStorageManager fileService,
            IContributionService contributionService,
            IContributionRootService contributionRootService,
            IMapper mapper,
            IStringLocalizer<SharedResource> localizer,
            INotificationService notificationService,
            IOptions<StoragePathTemplatesSettings> storagePathTemplatesSettings,
            IOptions<S3Settings> s3SettingsOptions,
            ILogger<ContentService> logger,
            IAmazonS3 amazonS3,
            IOptions<ClientUrlsSettings> clientUrlsOptions)
        {
            _unitOfWork = unitOfWork;
            _fileService = fileService;
            _contributionService = contributionService;
            _contributionRootService = contributionRootService;
            _mapper = mapper;
            _localizer = localizer;
            _storagePathTemplatesSettings = storagePathTemplatesSettings?.Value;
            _s3SettingsOptions = s3SettingsOptions?.Value;
            _notificationService = notificationService;
            _logger = logger;
            _amazonS3 = amazonS3;
            _urlSettings = clientUrlsOptions.Value;
        }

        public async Task<OperationResult> UploadAttachmentOnContribCreateAsync(FileDetails fileDetails, string contributionId = "", string duration = "")
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == fileDetails.AccountId);

            if (user == null)
            {
                return new OperationResult(false, _localizer["No user for content"]);
            }

            var documentId = Guid.NewGuid().ToString();

            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", fileDetails.AccountId)
                .Replace("{attachmentIdWithExtension}", $"{documentId}{fileDetails.Extension}");

            var fileUploadResult = await _fileService.UploadFileToStorageAsync(fileDetails.FileStream, _s3SettingsOptions.NonPublicBucketName, fileKey, fileDetails.ContentType);

            if (!fileUploadResult.Succeeded)
            {
                return OperationResult.Failure(fileUploadResult.Message);
            }

            var document = new Document();

            if (contributionId == String.Empty)
            {
                document = new Document
                {
                    Id = documentId,
                    DocumentKeyWithExtension = fileKey,
                    DocumentOriginalNameWithExtension = fileDetails.OriginalNameWithExtension,
                    ContentType = fileDetails.ContentType,
                    AttachementUrl = GeneratePresignedUrlForAttachments(fileKey)
                };
            }
            else
            {
                document = new DocumentViewModel
                {
                    Id = documentId,
                    DocumentKeyWithExtension = fileKey,
                    DocumentOriginalNameWithExtension = fileDetails.OriginalNameWithExtension,
                    ContentType = fileDetails.ContentType,
                    Duration = duration,
                    AttachementUrl = GeneratePresignedUrlForAttachments(fileKey)
                };
            }

            return new OperationResult(true, "Upload successful", document);
        }

        public async Task<OperationResult> UploadRecordedAttachmentOnContribCreateAsync(
            FileDetails fileDetails,
            int partNumber,
            bool isLastPart,
            string documentId,
            string contributionId,
            string duration)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution == null)
            {
                return OperationResult.Failure($"Unable to find contribution with Id: {contributionId}");
            }

            var cohealer = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == fileDetails.AccountId);

            if (cohealer == null)
            {
                return new OperationResult(false, _localizer["No user for content"]);
            }

            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", fileDetails.AccountId)
                .Replace("{attachmentIdWithExtension}", $"{documentId}{fileDetails.Extension}");

            var fileUploadResult = await _fileService.UploadObjectAsync(
                fileDetails.FileStream,
                _s3SettingsOptions.NonPublicBucketName,
                fileKey,
                fileDetails.ContentType,
                partNumber,
                isLastPart,
                fileDetails.UploadId,
                fileDetails.PrevETags);

            if (!fileUploadResult.Succeeded)
            {
                return fileUploadResult;
            }

            var returnedFileDetails = (FileDetails)fileUploadResult.Payload;
            if (string.IsNullOrEmpty(fileDetails.UploadId))
            {
                fileDetails.UploadId = returnedFileDetails?.UploadId;
            }

            fileDetails.PrevETags = returnedFileDetails?.PrevETags;

            if (isLastPart)
            {
                var document = new DocumentViewModel
                {
                    Id = documentId,
                    DocumentKeyWithExtension = fileKey,
                    DocumentOriginalNameWithExtension = fileDetails.OriginalNameWithExtension,
                    ContentType = fileDetails.ContentType,
                    Duration = duration,
                    Extension = fileDetails.Extension
                };

                return OperationResult.Success("Upload part successfully", document);
            }

            fileDetails.FileStream = null;
            return OperationResult.Success("Upload part successfully", fileDetails);
        }

        public async Task<OperationResult> DeleteSessionTimeRecording(string contributionId, string sessionTimeId, string roomId, string compositionFileName)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == contributionId);
            if ((contribution is SessionBasedContribution sessionBasedContribution))
            {
                var sessionTime = sessionBasedContribution.Sessions.SelectMany(x => x.SessionTimes).FirstOrDefault(x => x.Id == sessionTimeId);
                sessionTime.RecordingInfos.RemoveAll(x=>x.RoomId == roomId);
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, sessionBasedContribution);
            }
            else
            {
                if((contribution is ContributionOneToOne oneToOneContribution))
                {
                    var bookTime = oneToOneContribution.AvailabilityTimes.SelectMany(x => x.BookedTimes).FirstOrDefault(x => x.Id == sessionTimeId);
                    bookTime.RecordingInfos.RemoveAll(x => x.RoomId == roomId);
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, oneToOneContribution);
                }
            }
            return await _fileService.DeleteFileFromNonPublicStorageAsync($"Videos/Rooms/{roomId}/Compositions/{compositionFileName}");
        }

        public async Task<OperationResult> UploadAttachmentForExistedContribAsync(FileDetails fileDetails, AttachmentWithFileViewModel attachmentVm, int partNumber, bool isLastPart, string documentId)
        {
            var contribution = await _contributionRootService.GetOne(attachmentVm.ContributionId);

            if (contribution == null)
            {
                return OperationResult.Failure($"Unable to find contribution with Id: {attachmentVm.ContributionId}");
            }

            var cohealer = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == fileDetails.AccountId);

            if (cohealer == null)
            {
                return new OperationResult(false, _localizer["No user for content"]);
            }

            var fileKey = _storagePathTemplatesSettings.AttachmentPath
                .Replace("{accountId}", fileDetails.AccountId)
                .Replace("{attachmentIdWithExtension}", $"{documentId}{fileDetails.Extension}");

            var fileUploadResult = await _fileService.UploadObjectAsync(
                fileDetails.FileStream,
                _s3SettingsOptions.NonPublicBucketName,
                fileKey,
                fileDetails.ContentType,
                partNumber,
                isLastPart,
                fileDetails.UploadId,
                fileDetails.PrevETags);

            if (!fileUploadResult.Succeeded)
            {
                return fileUploadResult;
            }

            var returnedFileDetails = (FileDetails)fileUploadResult.Payload;
            if (string.IsNullOrEmpty(fileDetails.UploadId))
            {
                fileDetails.UploadId = returnedFileDetails?.UploadId;
            }

            fileDetails.PrevETags = returnedFileDetails?.PrevETags;

            if (isLastPart)
            {

                var document = new Document
                {
                    Id = documentId,
                    DocumentKeyWithExtension = fileKey,
                    DocumentOriginalNameWithExtension = fileDetails.OriginalNameWithExtension,
                    ContentType = fileDetails.ContentType
                };

                var addAttachmentResult =
                    await _contributionService.AddAttachmentToContribution(contribution, attachmentVm.SessionId, document);

                if (addAttachmentResult.Succeeded)
                {
                    var contributionCohealerVm = (ContributionBaseViewModel)addAttachmentResult.Payload;

                    if (contributionCohealerVm is SessionBasedContributionViewModel)
                    {
                        var podIds = ((SessionBasedContributionViewModel)contributionCohealerVm).Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                        ((SessionBasedContributionViewModel)contributionCohealerVm).Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
                    }

                    var participantsIds = contributionCohealerVm.GetBookedParticipantsIds();

                    var participantUserIds = (await _contributionService.GetParticipantsVmsAsync(contribution.Id)).Select(c => c.Id);

                    var participants = await _unitOfWork.GetRepositoryAsync<User>().Get(u => participantsIds.Contains(u.Id));
                    var participantsVm = _mapper.Map<IEnumerable<ParticipantViewModel>>(participants).ToList();
                    contributionCohealerVm.Participants = participantsVm;

                    try
                    {
                        GetAttachmentViewModel filemodel = new GetAttachmentViewModel()
                        {
                            ContributionId = attachmentVm.ContributionId,
                            SessionId= attachmentVm.SessionId,
                            DocumentId = documentId
                        };
                       var downloadLink = await  DownloadAttachmentAsync(filemodel, fileDetails.AccountId);
                        var redirectLink = $"{_urlSettings.WebAppUrl}/contribution-view/{attachmentVm.ContributionId}/sessions/{attachmentVm.SessionId}";
                       await _notificationService.SendNotificationAboutUploadedContent(
                            cohealer.AccountId,
                            participantUserIds,
                            document.DocumentOriginalNameWithExtension,
                            contribution.Title, downloadLink.Payload.ToString(),redirectLink);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during sending notification about uploading new content to a session");
                    }
                }
                else 
                {
                    await _fileService.DeleteFileFromNonPublicStorageAsync(fileKey);
                }
                return addAttachmentResult;
            }

            fileDetails.FileStream = null;
            return OperationResult.Success("Upload part successfully", fileDetails);
        }

        public async Task<OperationResult> DownloadAttachmentAsync(GetAttachmentViewModel attachmentVm, string requestorAccountId)
        {
            var contribution = await _contributionRootService.GetOne(attachmentVm.ContributionId);

            if (contribution == null)
            {
                return OperationResult.Failure($"Unable to find contribution with Id: {attachmentVm.ContributionId}");
            }

            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            var isOwnerOrAdmin = contribution.UserId == requestorUser.Id
                || requestorAccount.Roles.Contains(Roles.Admin)
                || requestorAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isOwnerOrAdmin)
            {
                var isContributionPurchasedByRequestor = await _contributionService.IsContributionPurchasedByUser(attachmentVm.ContributionId, requestorUser.Id);

                if (!isContributionPurchasedByRequestor)
                {
                    return OperationResult.Failure("Unable to download attachments. Please purchase contribution first");
                }
            }

            var getDocumentResult = _contributionService.GetAttachmentFromContribution(contribution, attachmentVm.SessionId, attachmentVm.DocumentId);

            if (!getDocumentResult.Succeeded)
            {
                return getDocumentResult;
            }

            var document = (Document)getDocumentResult.Payload;

            var fileDownloadResult = _fileService.GetPreSignUrlAsync(_s3SettingsOptions.NonPublicBucketName, document.DocumentKeyWithExtension, document.DocumentOriginalNameWithExtension);

            if (!fileDownloadResult.Succeeded)
            {
                return fileDownloadResult;
            }

            var filePath = (string)fileDownloadResult.Payload;


            return OperationResult.Success(string.Empty, filePath);
        }

        public async Task<OperationResult> DeleteAttachmentAsync(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId)
        {
            if (!attachmentVm.DocumentKeyWithExtension.Contains(attachmentVm.DocumentId))
            {
                return OperationResult.Failure("Document Id does not match document key");
            }

            var contribution = await _contributionRootService.GetOne(attachmentVm.ContributionId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (contribution.UserId != requestorUser.Id)
            {
                return OperationResult.Failure("Forbidden to delete attachments for not owned contribution");
            }

            var deleteFromStorageResult = await _fileService.DeleteFileFromNonPublicStorageAsync(attachmentVm.DocumentKeyWithExtension);

            if (!deleteFromStorageResult.Succeeded)
            {
                return OperationResult.Failure(deleteFromStorageResult.Message);
            }

            var deleteFromContributionResult =
                await _contributionService.RemoveAttachmentFromContribution(contribution, attachmentVm.SessionId, attachmentVm.DocumentId);

            return deleteFromContributionResult;
        }

        public async Task<OperationResult> UploadAvatarAsync(FileDetails fileDetails)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == fileDetails.AccountId);
            if (user?.AvatarUrl != null)
            {
                await _fileService.DeleteFileFromPublicStorageByUrlAsync(user.AvatarUrl);
            }

            var fileKey = $"{fileDetails.AccountId}/{Guid.NewGuid()}{fileDetails.Extension}";
            return await _fileService.UploadFileToStorageAsync(fileDetails.FileStream, _s3SettingsOptions.PublicBucketName, fileKey, fileDetails.ContentType);
        }

        public async Task<OperationResult> UploadPublicFileAsync(FileDetails fileDetails)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == fileDetails.AccountId);

            if (user != null)
            {
                var fileKey = $"{fileDetails.AccountId}/{Guid.NewGuid()}{fileDetails.Extension}";

                return await _fileService.UploadFileToStorageAsync(fileDetails.FileStream, _s3SettingsOptions.PublicBucketName, fileKey, fileDetails.ContentType);

            }

            return OperationResult.Failure(_localizer["No user for content"]);
        }

        public async Task<OperationResult> DeletePublicImageAsync(string imageUrl)
        {
            return await _fileService.DeleteFileFromPublicStorageByUrlAsync(imageUrl);
        }

        public async Task<OperationResult> UploadTestimonialAvatarAsync(FileDetails fileDetails, string contributionId, string previousUrl)
        {
            if (!string.IsNullOrEmpty(previousUrl))
            {
                await _fileService.DeleteFileFromPublicStorageByUrlAsync(previousUrl);
            }

            var fileKey = $"{contributionId}/{Guid.NewGuid()}{fileDetails.Extension}";
            return await _fileService.UploadFileToStorageAsync(fileDetails.FileStream, _s3SettingsOptions.PublicBucketName, fileKey, fileDetails.ContentType);
        }

        public async Task<OperationResult> UploadSessionTimeRecord(FileDetails fileDetails, AttachmentWithFileViewModel model, string sessionTimeId, string roomId, int partNumber, bool isLastPart, string documentId, int duration, string replyLink)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == model.ContributionId);

            var fileUploadResult = await _fileService.UploadObjectAsync(
                fileDetails.FileStream,
                _s3SettingsOptions.NonPublicBucketName,
                $"Videos/Rooms/{roomId}/Compositions/{fileDetails.OriginalNameWithExtension}",
                fileDetails.ContentType,
                partNumber,
                isLastPart,
                fileDetails.UploadId,
                fileDetails.PrevETags);

            if (!fileUploadResult.Succeeded)
            {
                return fileUploadResult;
            }

            var returnedFileDetails = (FileDetails)fileUploadResult.Payload;
            if (string.IsNullOrEmpty(fileDetails.UploadId))
            {
                fileDetails.UploadId = returnedFileDetails?.UploadId;
            }

            fileDetails.PrevETags = returnedFileDetails?.PrevETags;

            if (isLastPart)
            {
                if ((contribution is SessionBasedContribution sessionBasedContribution))
                {
                    var sessionTime = sessionBasedContribution.Sessions.SelectMany(x => x.SessionTimes).FirstOrDefault(x => x.Id == sessionTimeId);
                    sessionTime.RecordingInfos.Add(new Entity.EntitiesAuxiliary.Contribution.Recordings.RecordingInfo
                    {
                        RoomId = roomId.ToString(),
                        CompositionFileName = fileDetails.OriginalNameWithExtension,
                        Status = Entity.EntitiesAuxiliary.Contribution.Recordings.RecordingStatus.Available,
                        Duration = duration
                    });
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(model.ContributionId, sessionBasedContribution);
                    await NotifyParticipants(contribution, sessionTime, fileDetails.OriginalNameWithExtension, replyLink);
                    return OperationResult.Success(string.Empty, _mapper.Map<ContributionBaseViewModel>(contribution));
                }
                
            }

            fileDetails.FileStream = null;
            return OperationResult.Success("Upload part successfully", fileDetails);
        }
        public async Task<OperationResult> DownloadAttachmentSelfPacedAsync(GetAttachmentViewModel attachmentVm, string requestorAccountId)
        {
            var contribution = await _contributionRootService.GetOne(attachmentVm.ContributionId);
            if (contribution == null)
            {
                return OperationResult.Failure($"Unable to find contribution with Id: {attachmentVm.ContributionId}");
            }
            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);
            var isOwnerOrAdmin = contribution.UserId == requestorUser.Id
                || requestorAccount.Roles.Contains(Roles.Admin)
                || requestorAccount.Roles.Contains(Roles.SuperAdmin);
            if (!isOwnerOrAdmin)
            {
                var isContributionPurchasedByRequestor = await _contributionService.IsContributionPurchasedByUser(attachmentVm.ContributionId, requestorUser.Id);
                if (!isContributionPurchasedByRequestor)
                {
                    return OperationResult.Failure("Unable to download attachments. Please purchase contribution first");
                }
            }
            var getDocumentResult = _contributionService.GetAttachmentFromContributionSelfPacedSessions(contribution, attachmentVm.DocumentId);
            if (!getDocumentResult.Succeeded)
            {
                return getDocumentResult;
            }
            var document = (Document)getDocumentResult.Payload;
            string filePath = string.Empty;
            if (document != null)
            {
                filePath = GeneratePresignedUrlForAttachments(document.DocumentKeyWithExtension);
            }
            return OperationResult.Success(string.Empty, filePath);
        }
        public async Task<OperationResult> DeleteAttachmentSelfPacedAsync(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId, bool isVideo)
        {
            if (!attachmentVm.DocumentKeyWithExtension.Contains(attachmentVm.DocumentId))
            {
                return OperationResult.Failure("Document Id does not match document key");
            }
            var contribution = await _contributionRootService.GetOne(attachmentVm.ContributionId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);
            if (contribution.UserId != requestorUser.Id)
            {
                return OperationResult.Failure("Forbidden to delete attachments for not owned contribution");
            }
            var deleteFromStorageResult = await _fileService.DeleteFileFromNonPublicStorageAsync(attachmentVm.DocumentKeyWithExtension);
            if (!deleteFromStorageResult.Succeeded)
            {
                return OperationResult.Failure(deleteFromStorageResult.Message);
            }
            var deleteFromContributionResult =
                await _contributionService.RemoveAttachmentFromContributionSessionTimes(contribution, attachmentVm.DocumentId, isVideo);
            return deleteFromContributionResult;
        }
        public async Task<OperationResult> DeleteSelfpacedVideoOnContributionCreation(AttachmentWithKeyViewModel attachmentVm, string requestorAccountId)
        {
            if (!attachmentVm.DocumentKeyWithExtension.Contains(attachmentVm.DocumentId))
            {
                return OperationResult.Failure("Document Id does not match document key");
            }
            var deleteFromStorageResult = await _fileService.DeleteFileFromNonPublicStorageAsync(attachmentVm.DocumentKeyWithExtension);
            if (!deleteFromStorageResult.Succeeded)
            {
                return OperationResult.Failure(deleteFromStorageResult.Message);
            }
            return deleteFromStorageResult;
        }
        public async Task<OperationResult> RemoveCustomLogo(string accountId, string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return OperationResult.Failure("There is no image to delete.");
            }
            await _fileService.DeleteFileFromPublicStorageByUrlAsync(imageUrl);

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.AccountId == accountId);
            user.CustomLogo = String.Empty;

            await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);

            return OperationResult.Success("Custom logo removed sucessfully.");
        }
        private async Task NotifyParticipants(ContributionBase contribution, SessionTime sessionTime, string fileName, string replyLink)
        {
            var participantInfos = sessionTime.ParticipantsIds;
            if (!string.IsNullOrEmpty(sessionTime.PodId))
            {
                var pod = await _unitOfWork.GetRepositoryAsync<Pod>().GetOne(x => x.Id == sessionTime.PodId);
                if (pod.ClientIds != null && pod.ClientIds.Any())
                {
                    participantInfos.AddRange(pod.ClientIds);
                }
            }

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            await _notificationService.SendNotificationAboutNewRecording(null, participantInfos, fileName, contributionVm, sessionTime.Id);
        }
        private string GeneratePresignedUrlForAttachments(string key)
        {
            var duration = TimeSpan.FromDays(1);
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _s3SettingsOptions.NonPublicBucketName,
                Key = $"{key}",
                Expires = DateTime.UtcNow.Add(duration)
            };
            return _amazonS3.GetPreSignedURL(request);
        }
        
        public async Task<Stream> GetFileFromS3Async(string fileLink)
        {
            if (!string.IsNullOrEmpty(fileLink))
            {
                var fileKey = GetFileKey(fileLink);
                if (!string.IsNullOrEmpty(fileKey))
                {
                    var streamobject = await _fileService.DownloadFileFromStorageAsync(_s3SettingsOptions.PublicBucketName, fileKey);
                    if (streamobject != null && streamobject.Payload != null)
                    {
                        return streamobject.Payload as Stream;
                    }
                }
            }
            return null;
        }

        public string GetFileKey(string fileLink)
        {
            string fileKey = null;

            if(!string.IsNullOrEmpty(fileLink))
            {
                fileKey = fileKeyRegex.Match(fileLink).ToString();
            }

            return fileKey;
        }
    } 
}
