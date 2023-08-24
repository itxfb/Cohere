using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using AutoMapper;
using Cohere.Domain.Messages;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cohere.Domain.Service.Workers
{
    public class VideoCompletedService : BackgroundService
    {
        private readonly ILogger<VideoCompletedService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAmazonSQS _amazonSQS;
        private readonly IContributionRootService _contributionRootService;
        private readonly INotificationService _notificationService;
        private readonly string _videoCompletedQueueUrl;

        public VideoCompletedService(
            ILogger<VideoCompletedService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IAmazonSQS amazonSQS,
            IContributionRootService contributionRootService,
            INotificationService notificationService,
            IOptions<SqsSettings> sqsSettings)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _amazonSQS = amazonSQS;
            _contributionRootService = contributionRootService;
            _notificationService = notificationService;
            _videoCompletedQueueUrl = sqsSettings.Value.VideoCompletedQueueUrl;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _videoCompletedQueueUrl,
                        WaitTimeSeconds = 5
                    };

                    var messages = (await _amazonSQS.ReceiveMessageAsync(request, cancellationToken)).Messages;
                    foreach (var message in messages)
                    {
                        try
                        {
                            _logger.LogInformation("Start processing message {message} | {time}", message.Body, DateTime.UtcNow);
                            var video = JsonSerializer.Deserialize<VideoCompletedMessage>(message.Body);
                            await UpdateRoomRecordingInfo(video.ContributionId, video.RoomId, video.CompositionFileName, video.CompositionDuration);
                            await _amazonSQS.DeleteMessageAsync(_videoCompletedQueueUrl, message.ReceiptHandle);

                            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == video.ContributionId);

                            try
                            {
                                var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
                                if (contributionVm is SessionBasedContributionViewModel)
                                {
                                    var podIds = ((SessionBasedContributionViewModel)contributionVm).Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                                    ((SessionBasedContributionViewModel)contributionVm).Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
                                }
                                var participantUserIds = contributionVm.RoomsWithParticipants[video.RoomId];

                                await _notificationService.SendNotificationAboutNewRecording(
                                    video.RoomId,
                                    participantUserIds,
                                    video.CompositionFileName,
                                    contributionVm,"");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during sending notification about uploading new content to a session");
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Error during updating video info");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error during updating video info");
                }
            }
        }

        public async Task UpdateRoomRecordingInfo(string contributionId, string roomId, string fileName, int? duration)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            var contributionViewModel = _mapper.Map<ContributionBaseViewModel>(contribution);

            contributionViewModel.UpdateRecordingsInfo(roomId, fileName, duration);

            var updatedContribution = _mapper.Map<ContributionBase>(contributionViewModel);

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, updatedContribution);
        }
    }
}
