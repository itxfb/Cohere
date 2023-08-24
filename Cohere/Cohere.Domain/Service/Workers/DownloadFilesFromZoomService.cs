using Amazon.SQS;
using Amazon.SQS.Model;
using AutoMapper;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Workers
{
    [Obsolete]
    public class DownloadFilesFromZoomService : IHostedService, IDisposable
    {
        private readonly ILogger<DownloadFilesFromZoomService> _logger;
        private Timer _timer;
        private bool _disposed;
        private readonly IUnitOfWork _unitOfWork;
        private Task doWorkTask;
        private readonly IFileStorageManager _fileStorageManager;
        private readonly S3Settings _s3SettingsOptions;
        private readonly INotificationService _notificationService;
        private readonly IAmazonSQS _amazonSqs;
        private readonly SqsSettings _sqsOptions;
        private readonly IMapper _mapper;

        public DownloadFilesFromZoomService(ILogger<DownloadFilesFromZoomService> logger, IUnitOfWork unitOfWork,
            IFileStorageManager fileStorageManager, IOptions<S3Settings> s3SettingsOptions, INotificationService notificationService, IAmazonSQS amazonSqs, IOptions<SqsSettings> sqsOptions, IMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _fileStorageManager = fileStorageManager;
            _s3SettingsOptions = s3SettingsOptions?.Value;
            _notificationService = notificationService;
            _amazonSqs = amazonSqs;
            _sqsOptions = sqsOptions?.Value;
            _mapper = mapper;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(5));

            return Task.CompletedTask;
        }


        private void ExecuteTask(object state)
        {
            doWorkTask = DoWork();
        }

        // added when upgraded .net core 3.1 to .net 6		
        // Todo: change WebClient to HttpClient (then remove Obsolete attribute from the class)
        [Obsolete]
        private async Task DoWork()
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _sqsOptions.ZoomVideoCompletedQueueUrl,
                    MessageAttributeNames = new List<string> { "_.*" },
                };
                List<Message> messages = (await _amazonSqs.ReceiveMessageAsync(request)).Messages;
                _logger.LogError($"Zoom - Message Read Started - Count: {messages.Count}");

                foreach (var message in messages)
                {
                    var completedZoomRecord = JsonConvert.DeserializeObject<ZoomRecordCompletedModel>(message.Body);
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.ZoomMeetigsIds.Contains(completedZoomRecord.payload.@object.id));

                    _logger.LogError($"Zoom - Contribution Id: {contribution?.Id} found for meeting Id: {completedZoomRecord?.payload?.@object?.id}");

                    var contributionChanged = false;

                    if (contribution is SessionBasedContribution vm)
                    {
                        foreach (var session in vm.Sessions)
                        {
                            foreach (var sessionTime in session.SessionTimes)
                            {
                                if (sessionTime.ZoomMeetingData.MeetingId == completedZoomRecord.payload.@object.id)
                                {
                                    _logger.LogError($"Zoom - session Time Meeting Id: {sessionTime.ZoomMeetingData.MeetingId} equals to meeting Id in message: {completedZoomRecord?.payload?.@object?.id}");
                                    using (var client = new WebClient())
                                    {
                                        sessionTime.RecordingInfos.AddRange(await DownloadVideoRecording(sessionTime, session, completedZoomRecord, client, "video/mp4"));
                                        sessionTime.ZoomMeetingData.ChatFiles.AddRange(await DownloadRecording(sessionTime, session, completedZoomRecord, client, "text/plain", $"{session.Title} Zoom Chat"));

                                        contributionChanged = true;
                                        try
                                        {
                                            await NotifyParticipants(contribution, sessionTime, $"{session.Title}.MP4");
                                        }
                                        catch (Exception ex)
                                        {

                                            _logger.LogError(ex, $"Zoom Error - Error in notifying participants for meeting Id: {completedZoomRecord?.payload?.@object?.id}");
                                        }
                                    }
                                }
                            }
                        }
                        if (contributionChanged)
                        {
                            _logger.LogError($"Zoom - Contribution changed and updating in database contribId: {contribution.Id}, Zoom meeting Id: {completedZoomRecord?.payload?.@object?.id}");
                            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                        }
                    }
                    else
                    {
                        if(contribution is ContributionOneToOne oneToOnevm)
                        {
                            foreach (var availableTime in oneToOnevm.AvailabilityTimes)
                            {
                                foreach(var bookTime in availableTime.BookedTimes)
                                {
                                    if (bookTime.ZoomMeetingData.MeetingId == completedZoomRecord.payload.@object.id)
                                    {
                                        using (var client = new WebClient())
                                        {
                                            Session obj = new Session();
                                            obj.Title = "Session";
                                            bookTime.RecordingInfos.AddRange(await DownloadVideoRecording(null, obj, completedZoomRecord, client, "video/mp4"));
                                            bookTime.ZoomMeetingData.ChatFiles.AddRange(await DownloadRecordingForOneToOne(bookTime, completedZoomRecord, client, "text/plain", "Session Zoom Chat"));

                                            contributionChanged = true;
                                            //await NotifyParticipants(contribution, sessionTime, $"{session.Title}.MP4");
                                        }
                                    }
                                }
                            }

                            if (contributionChanged)
                            {
                                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                            }

                        }
                    }
                    _logger.LogError($"Zoom - Deleteing message from queue for Zoom meeting Id: {completedZoomRecord?.payload?.@object?.id}");
                    await _amazonSqs.DeleteMessageAsync(_sqsOptions.ZoomVideoCompletedQueueUrl, message.ReceiptHandle);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _timer?.Dispose();
            }

            _disposed = true;
        }

        private async Task<List<string>> DownloadRecording(SessionTime sessionTime, Session session, ZoomRecordCompletedModel completedZoomRecord, WebClient client, string contentType, string fileName = null)
        {
            var result = new List<string>();
            var counter = 0;
            foreach (var recordingFile in completedZoomRecord.payload.@object.recording_files.Where(x => x.recording_type == "chat_file"))
            {
                var content = client.DownloadData($"{completedZoomRecord.payload.@object.recording_files.First(x => x.recording_type == "chat_file").download_url}/?access_token={completedZoomRecord.download_token}");
                using (var stream = new MemoryStream(content))
                {
                    var partName = fileName;
                    if (counter > 0)
                    {
                        partName = $"{partName} ({counter})";
                    }
                    partName = $"{partName}.{recordingFile.file_extension}";
                    var fileUrl = await _fileStorageManager.UploadFileToStorageAsync(stream, _s3SettingsOptions.NonPublicBucketName, $"{sessionTime.ZoomMeetingData.MeetingId}/{partName}", contentType);
                    result.Add(partName);
                }
                counter++;
            }

            return result;
        }
         private async Task<List<string>> DownloadRecordingForOneToOne(BookedTime bookTime, ZoomRecordCompletedModel completedZoomRecord, WebClient client, string contentType, string fileName = null)
        {
            var result = new List<string>();
            var counter = 0;
            foreach (var recordingFile in completedZoomRecord.payload.@object.recording_files.Where(x => x.recording_type == "chat_file"))
            {
                var content = client.DownloadData($"{completedZoomRecord.payload.@object.recording_files.First(x => x.recording_type == "chat_file").download_url}/?access_token={completedZoomRecord.download_token}");
                using (var stream = new MemoryStream(content))
                {
                    var partName = fileName;
                    if (counter > 0)
                    {
                        partName = $"{partName} ({counter})";
                    }
                    partName = $"{partName}.{recordingFile.file_extension}";
                    var fileUrl = await _fileStorageManager.UploadFileToStorageAsync(stream, _s3SettingsOptions.NonPublicBucketName, $"{bookTime.ZoomMeetingData.MeetingId}/{partName}", contentType);
                    result.Add(partName);
                }
                counter++;
            }

            return result;
        }

        private async Task<List<RecordingInfo>> DownloadVideoRecording(SessionTime sessionTime, Session session, ZoomRecordCompletedModel completedZoomRecord, WebClient client, string contentType)
        {
            var result = new List<RecordingInfo>();
            var recordingFiles = completedZoomRecord.payload.@object.recording_files.Where(x => x.file_type == "MP4");
            foreach (var file in recordingFiles.OrderBy(x => x.recording_start))
            {
                var content = client.DownloadData($"{file.download_url}/?access_token={completedZoomRecord.download_token}");
                using (var stream = new MemoryStream(content))
                {
                    var roomId = Guid.NewGuid();
                    var fileName = $"Videos/Rooms/{roomId}/Compositions/{session.Title}.{file.file_extension}";
                    var fileUrl = await _fileStorageManager.UploadFileToStorageAsync(stream, _s3SettingsOptions.NonPublicBucketName, $"{fileName}", contentType);
                    result.Add(new RecordingInfo
                    {
                        CompositionFileName = $"{session.Title}.{file.file_extension}",
                        RoomId = roomId.ToString(),
                        Duration = (int)Math.Ceiling((file.recording_end - file.recording_start).TotalSeconds),
                        Status = RecordingStatus.Available
                    });
                }
            }

            return result;
        }

        public async Task NotifyParticipants(ContributionBase contribution, SessionTime sessionTime, string fileName)
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
    }
}
