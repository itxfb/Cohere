using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Twilio;
using Twilio.Exceptions;
using Twilio.Http;

namespace Cohere.Domain.Service
{
    public class RecordingService : IRecordingService
    {
        private const string Include = "include";
        private const string Exclude = "exclude";
        private readonly TwilioSettings _twilioSettings;
        private readonly SecretsSettings _secretSettings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<RecordingService> _logger;

        public RecordingService(
            IOptions<TwilioSettings> twilioSettings,
            IOptions<SecretsSettings> secretSettings,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<RecordingService> logger)
        {
            _twilioSettings = twilioSettings.Value;
            _secretSettings = secretSettings.Value;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<OperationResult> GetCurrentRoomStatus(RecordingRequestModel request, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var contribution =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                    .GetOne(c => c.Id == request.ContributionId);
            try
            {               
                var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);

                if (contributionVm is SessionBasedContributionViewModel)
                {
                    var podIds = ((SessionBasedContributionViewModel)contributionVm).Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                    ((SessionBasedContributionViewModel)contributionVm).Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
                }

                if (!contributionVm.IsOwnerOrPartnerOrParticipant(user.Id, request.RoomCid))
                {
                    return OperationResult.Failure("You have no access to conduct this operation");
                }

                TwilioClient.Init(_twilioSettings.TwilioAccountSid, _secretSettings.TwilioAccountAuthToken);

                var twilioRequest =
                    new Request(HttpMethod.Get, $"https://video.twilio.com/v1/Rooms/{request.RoomCid}/RecordingRules");

                var result = await TwilioClient.GetRestClient().RequestAsync(twilioRequest);
                var parsedResult = JsonConvert.DeserializeObject<RecordingRulesResponse>(result.Content);

                var status = parsedResult.Rules.FirstOrDefault()?.Type == Include
                    ? ToggleStatus.Started
                    : ToggleStatus.Stopped;

                return OperationResult.Success("Request completed.", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, @$"RecordingService.GetCurrentRoomStatus method exception occured: {ex.Message} {Environment.NewLine} 
                For User_Id: {user.Id} - Contribution_ID: {contribution.Id}");

                return OperationResult.Failure($"Unable to complete the request for the user: {user.Id} {ex.Message}");
            }            
        }

        public async Task<OperationResult> ToggleRecording(
            RecordingRequestModel request,
            string accountId,
            bool renewRequest)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var contribution =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                    .GetOne(c => c.Id == request.ContributionId);

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);

            if (!contributionVm.IsOwnerOrPartner(user.Id))
            {
                return OperationResult<string>.Failure("You have no access to conduct this operation");
            }

            TwilioClient.Init(_twilioSettings.TwilioAccountSid, _secretSettings.TwilioAccountAuthToken);

            var twilioRequest =
                new Request(HttpMethod.Post, $"https://video.twilio.com/v1/Rooms/{request.RoomCid}/RecordingRules");

            var actionType = renewRequest ? Include : Exclude;

            twilioRequest.AddPostParam("Rules", $"[{{\"type\": \"{actionType}\", \"all\": \"true\"}}]");

            await TwilioClient.GetRestClient().RequestAsync(twilioRequest);
            
            var resultType = renewRequest ? ToggleStatus.Started : ToggleStatus.Stopped;

            if (resultType == ToggleStatus.Started)
            {
                if (contributionVm.GetRecordingInfo(request.RoomCid) is null)
                {
                    var targetClass = contributionVm.ClassesInfo.Values.FirstOrDefault(e =>
                        e.VideoRoomContainer?.VideoRoomInfo?.RoomId == request.RoomCid);
                    
                    var videoRoomInfo = targetClass.VideoRoomContainer.VideoRoomInfo;
                    
                    targetClass.RecordingInfos.Add(new RecordingInfo()
                    {
                        RoomId = videoRoomInfo.RoomId,
                        RoomName = videoRoomInfo.RoomName,
                        DateCreated = videoRoomInfo.DateCreated
                    });

                    var updatedContrib = _mapper.Map<ContributionBase>(contributionVm);

                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(updatedContrib.Id, updatedContrib);
                }
            }

            return OperationResult.Success("Request completed.", $"{resultType}");
        }
    }
}