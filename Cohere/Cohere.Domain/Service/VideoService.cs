using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Messages;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Exceptions;
using Twilio.Http;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Video.V1;
using static Twilio.Rest.Video.V1.CompositionResource;
using Twilio.Rest.Video.V1.Room;
using ParticipantStatus = Twilio.Rest.Video.V1.Room.ParticipantResource.StatusEnum;
using System.Text.RegularExpressions;
using Cohere.Domain.Models.Payment;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Cohere.Domain.Service
{
    public class VideoService : IVideoService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IContributionService _contributionService;
        private readonly IContributionRootService _contributionRootService;
        private readonly ILogger<VideoService> _logger;
        private readonly IAccountManager _accountManager;
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly IAmazonSQS _amazonSQS;
        private readonly string _accountSid;
        private readonly string _apiSid;
        private readonly string _apiSecret;
        private readonly string _authToken;
        private readonly int _videoTokenLifetimeSec;
        private readonly string _videoWebHookUrl;
        private readonly string _contributionWebHookUrl;
        private readonly string _sqsQueueUrl;
        private readonly string _distributionName;
        private readonly ICommonService _commonService;

        private readonly object _defaultLayout = new
        {
            grid = new
            {
                video_sources = new List<string> {"*"}
            }
        };

        private string BuildContributionWebHookUrl(string contributionId) =>
            $"{_contributionWebHookUrl}/{contributionId}";

        private string BuildTwilioEventWebHookUrl(string contributionId) =>
            $"{_videoWebHookUrl}/{contributionId}";

        public VideoService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IContributionService contributionService,
            IContributionRootService contributionRootService,
            ILogger<VideoService> logger,
            IAccountManager accountManager,
            IAmazonS3 amazonS3,
            string bucketName,
            IAmazonSQS amazonSQS,
            string accountSid,
            string apiSid,
            string apiSecret,
            string authToken,
            int videoTokenLifetimeSec,
            string videoWebHookUrl,
            string contributionWebHookUrl,
            string sqsQueueUrl,
            string distributionName,
            ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _contributionService = contributionService;
            _contributionRootService = contributionRootService;
            _logger = logger;
            _accountManager = accountManager;
            _amazonS3 = amazonS3;
            _bucketName = bucketName;
            _amazonSQS = amazonSQS;
            _accountSid = accountSid;
            _apiSid = apiSid;
            _apiSecret = apiSecret;
            _authToken = authToken;
            _videoTokenLifetimeSec = videoTokenLifetimeSec;
            _videoWebHookUrl = videoWebHookUrl;
            _contributionWebHookUrl = contributionWebHookUrl;
            _sqsQueueUrl = sqsQueueUrl;
            _distributionName = distributionName;
            _commonService = commonService;
        }

        public async Task<OperationResult> GetClientTokenAsync(
            GetVideoTokenViewModel viewModel,
            string requesterAccountId)
        {
            var contributionVm =
                _mapper.Map<ContributionBaseViewModel>(await _contributionRootService.GetOne(viewModel.ContributionId));

            if (contributionVm == null)
            {
                return OperationResult.Failure("Unable to find contribution to get access to");
            }

            var requesterAndAuthor = await _unitOfWork.GetRepositoryAsync<User>()
                .Get(u => u.AccountId == requesterAccountId || u.Id == contributionVm.UserId);
            var requesterAndAuthorList = requesterAndAuthor.ToList();
            var requesterUser = requesterAndAuthorList.First(u => u.AccountId == requesterAccountId);
            var authorUser = requesterAndAuthorList.First(u => u.Id == contributionVm.UserId);

            await FillPodsForSessionContribution(contributionVm);

            var getRoomInfoResult = contributionVm.GetRoomInfoFromClass(viewModel.ClassId);
            if (getRoomInfoResult.Failed)
            {
                return OperationResult.Failure(getRoomInfoResult.Message);
            }

            var roomInfo = getRoomInfoResult.Payload;

            if (roomInfo is null)
            {
                return OperationResult.Failure(
                    "Unable to get video room information. Perhaps the room is not created yet");
            }

            if (roomInfo.IsRunning == false)
            {
                return OperationResult.Failure(
                    "Unable to get information to connect video room. The room has already been closed");
            }

            var isParticipantInRequestedClass =
                contributionVm.IsParticipantInClass(viewModel.ClassId, requesterUser.Id);

            if (!isParticipantInRequestedClass)
            {
                return OperationResult.Failure("Unable issue token. User did not book the requested session time");
            }

            var grants = new HashSet<IGrant>
            {
                new VideoGrant
                {
                    Room = roomInfo.RoomId
                }
            };
            var userIdentity = viewModel.IdentityName;

            try
            {
                userIdentity = await GetUserIdentity(requesterUser.Id, contributionVm, viewModel.ClassId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during obtaining unique user identity");
            }

            var token = new Token(
                _accountSid,
                _apiSid,
                _apiSecret,
                userIdentity,
                expiration: DateTime.UtcNow.AddSeconds(_videoTokenLifetimeSec),
                nbf: DateTime.UtcNow,
                grants: grants);

            var tokenModel = new GetTokenViewModel
            {
                AuthorFullName = $"{authorUser.FirstName} {authorUser.LastName}",
                ContributionName = contributionVm.Title,
                ClassName = roomInfo.RoomName,
                Token = token.ToJwt()
            };

            return OperationResult.Success(string.Empty, tokenModel);
        }

        public async Task<OperationResult> CreateRoom(GetVideoTokenViewModel viewModel, string cohealerAccountId)
        {
            var contributionVm =
                _mapper.Map<ContributionBaseViewModel>(await _contributionRootService.GetOne(viewModel.ContributionId));

            if (contributionVm is null)
            {
                return OperationResult.Failure(
                    $"Room is not created. Unable to find contribution with id: {viewModel.ContributionId}");
            }

            var author = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == cohealerAccountId);
            var isPartner = contributionVm.Partners.Any(x => x.UserId == author.Id);

            if (author.Id != contributionVm.UserId && !isPartner)
            {
                return OperationResult.Failure("Room is not created. Contribution requested has different author");
            }

            RoomResource room = null;
            string className = null;
            var webHookUrl = BuildTwilioEventWebHookUrl(contributionVm.Id);
            var isRoomExistsAndRunning = false;

            _logger.Log(LogLevel.Information, $"VideoService.CreateRoom method WebHookURL: {webHookUrl}");

            try
            {
                TwilioClient.Init(_accountSid, _authToken);

                if (contributionVm is ContributionOneToOneViewModel contributionOneToOneVm)
                {
                    var bookedTime = contributionOneToOneVm.AvailabilityTimes
                        .SelectMany(at => at.BookedTimes)
                        .FirstOrDefault(bt => bt.Id == viewModel.ClassId);

                    var participantId = bookedTime?.ParticipantId;

                    if (!string.IsNullOrEmpty(participantId))
                    {
                        if (bookedTime.VideoRoomInfo != null && bookedTime.VideoRoomInfo.IsRunning)
                        {
                            var options = new FetchRoomOptions(bookedTime.VideoRoomInfo.RoomId);
                            room = await RoomResource.FetchAsync(options);
                            className = bookedTime.VideoRoomInfo.RoomName;
                            isRoomExistsAndRunning = true;
                            if (room.Status != RoomResource.RoomStatusEnum.InProgress)
                            {
                                isRoomExistsAndRunning = false;
                                var participant = await _unitOfWork.GetRepositoryAsync<User>()
                                    .GetOne(u => u.Id == participantId);
                                className = $"{participant.FirstName} {participant.LastName}";
                                room = await RoomResource.CreateAsync(
                                    type: RoomResource.RoomTypeEnum.GroupSmall,
                                    statusCallback: new Uri(webHookUrl),
                                    statusCallbackMethod: HttpMethod.Post,
                                    recordParticipantsOnConnect: viewModel.RecordParticipantsOnConnect);
                            }
                        }
                        else
                        {
                            var participant = await _unitOfWork.GetRepositoryAsync<User>()
                                .GetOne(u => u.Id == participantId);
                            className = $"{participant.FirstName} {participant.LastName}";
                            room = await RoomResource.CreateAsync(
                                type: RoomResource.RoomTypeEnum.GroupSmall,
                                statusCallback: new Uri(webHookUrl),
                                statusCallbackMethod: HttpMethod.Post,
                                recordParticipantsOnConnect: viewModel.RecordParticipantsOnConnect);
                        }
                    }
                    else
                    {
                        return OperationResult.Failure(
                            $"Room is not created. Unable to find booked time with participant id {participantId} in assigned");
                    }
                }

                if (contributionVm is SessionBasedContributionViewModel sessionBasedContributionViewModel)
                {
                    var podIds = ((SessionBasedContributionViewModel)contributionVm).Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                    ((SessionBasedContributionViewModel)contributionVm).Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();

                    var session =
                        sessionBasedContributionViewModel.Sessions.FirstOrDefault(s =>
                            s.SessionTimes.Any(st => st.Id == viewModel.ClassId));
                    SessionTime currentSessionTime = null;
                    if (session != null)
                    {
                        currentSessionTime = session.SessionTimes.FirstOrDefault(st => st.Id == viewModel.ClassId);
                        className = session.Title;
                    }

                    if (!string.IsNullOrEmpty(className))
                    {
                        if (currentSessionTime?.VideoRoomInfo != null &&
                            currentSessionTime.VideoRoomInfo.IsRunning)
                        {
                            var options = new FetchRoomOptions(currentSessionTime.VideoRoomInfo.RoomId);
                            room = await RoomResource.FetchAsync(options);
                            isRoomExistsAndRunning = true;

                            if (room.Status != RoomResource.RoomStatusEnum.InProgress)
                            {
                                isRoomExistsAndRunning = false;
                                room = await RoomResource.CreateAsync(
                                    type: RoomResource.RoomTypeEnum.Group,
                                    statusCallback: new Uri(webHookUrl),
                                    statusCallbackMethod: HttpMethod.Post,
                                    recordParticipantsOnConnect: viewModel.RecordParticipantsOnConnect);
                            }
                        }
                        else
                        {
                            room = await RoomResource.CreateAsync(
                                type: RoomResource.RoomTypeEnum.Group,
                                statusCallback: new Uri(webHookUrl),
                                statusCallbackMethod: HttpMethod.Post,
                                recordParticipantsOnConnect: viewModel.RecordParticipantsOnConnect);
                        }
                    }
                    else
                    {
                        return OperationResult.Failure(
                            $"Room is not created. Unable to find session or session time with id {viewModel.ClassId} in database");
                    }
                }
            }
            catch (ApiException ex)
            {
                return OperationResult.Failure(
                    "Unable to create room due to video vendor error. Please contact support and provide this details:" +
                    $"vendor error code: {ex.Code}; " +
                    $"vendor additional error info: {ex.MoreInfo}; " +
                    $"UTC time of attempt {DateTime.UtcNow:MM/dd/yyyy HH:mm:ss}; ");
            }
            catch (TwilioException)
            {
                return OperationResult.Failure(
                    $"Unable to create room due to video vendor error. Please contact support and provide this UTC time for your creation attempt {DateTime.UtcNow:MM/dd/yyyy HH:mm:ss}");
            }

            if (room != null && room.Status == RoomResource.RoomStatusEnum.InProgress)
            {
                var assignTimeResult = OperationResult.Success(string.Empty);

                if (!isRoomExistsAndRunning)
                {
                    var roomInfo = new VideoRoomInfo
                    {
                        RoomId = room.Sid,
                        RoomName = className,
                        IsRunning = true,
                        RecordParticipantsOnConnect = room.RecordParticipantsOnConnect.GetValueOrDefault(),
                        DateCreated = room.DateCreated,
                        CreatorId = author.Id
                    };
                    assignTimeResult =
                        await _contributionService.AssignRoomIdAndNameToClass(
                            contributionVm,
                            roomInfo,
                            viewModel.ClassId);
                }

                if (assignTimeResult.Succeeded)
                {
                    var grants = new HashSet<IGrant>
                    {
                        new VideoGrant
                        {
                            Room = room.Sid
                        }
                    };
                    var authorFullName = $"{author.FirstName} {author.LastName}";
                    var authorIdentity = authorFullName;
                    try
                    {
                        authorIdentity = await GetUserIdentity(author.Id, contributionVm, viewModel.ClassId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "error during obtaining unique user identity");
                    }

                    var token = new Token(
                        _accountSid,
                        _apiSid,
                        _apiSecret,
                        identity: authorIdentity,
                        expiration: DateTime.UtcNow.AddSeconds(_videoTokenLifetimeSec),
                        nbf: DateTime.UtcNow,
                        grants: grants);

                    var resultModel = new CreatedRoomAndGetTokenViewModel
                    {
                        Room = room,
                        Token = token.ToJwt(),
                        AuthorFullName = authorFullName,
                        ContributionName = contributionVm.Title,
                        ClassName = className
                    };

                    return OperationResult.Success(string.Empty, resultModel);
                }

                room = await RoomResource.UpdateAsync(new UpdateRoomOptions(
                    room.Sid,
                    RoomResource.RoomStatusEnum.Completed));
                return OperationResult.Failure(
                    $"Room created and closed. {assignTimeResult.Message}. Current room status: {room.Status}");
            }

            return OperationResult.Failure(
                "Room is not created. Try again later and if the problem persists contact support");
        }

        public async Task<OperationResult> DeleteRoom(DeleteRoomInfoViewModel viewModel, string requesterAccountId)
        {
            var contribution = await _contributionRootService.GetOne(viewModel.ContributionId);

            if (contribution == null)
            {
                return OperationResult.Failure("Unable to delete a room. Associated contribution is not found");
            }

            var requesterUser = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(u => u.AccountId == requesterAccountId);
            var requesterAccount =
                await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requesterAccountId);

            var isHavePermissionsToDelete = requesterUser.Id == contribution.UserId ||
                                            contribution.Partners.Any(x =>
                                                x.IsAssigned && x.UserId == requesterUser.Id) ||
                                            requesterAccount.Roles.Contains(Roles.Admin) ||
                                            requesterAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isHavePermissionsToDelete)
            {
                return OperationResult.Failure("Sorry, you don't have enough permissions to delete the room");
            }

            RoomResource room;
            try
            {
                TwilioClient.Init(_accountSid, _authToken);

                var options = new FetchRoomOptions(viewModel.RoomId);
                var roomToCheckStatus = await RoomResource.FetchAsync(options);

                if (roomToCheckStatus.Status == RoomResource.RoomStatusEnum.Completed)
                {
                    return OperationResult.Failure("Unable to delete the room. Room has already been deleted");
                }

                // check if a coach or a partner coach already in the room
                if(contribution is ContributionCourse contributionCourse)
				{
                    if (contributionCourse.Partners?.Count() > 0)
                    {
                        var currentParticipants = await ParticipantResource.ReadAsync(viewModel.RoomId, ParticipantStatus.Connected);
                        if(currentParticipants?.Count() > 0)
						{
                            List<string> coachesNames = new List<string>();
                            List<string> currentParticipantsNames = currentParticipants
                                .Select(p => Regex.Replace(p.Identity, @" \((.*?)\)", string.Empty)?.Trim())?
                                .ToList();
                            foreach (var partner in contributionCourse.Partners)
							{
                                var partnerUser = await _unitOfWork.GetRepositoryAsync<User>()
                                    .GetOne(u => u.Id == partner.UserId);
                                if(!string.IsNullOrEmpty(partnerUser?.FirstName))
								{
                                    coachesNames.Add((partnerUser.FirstName + " " + partnerUser.LastName).Trim());
                                }
                            }
                            var coachUser = await _unitOfWork.GetRepositoryAsync<User>()
                                    .GetOne(u => u.Id == contributionCourse.UserId);
                            if (!string.IsNullOrEmpty(coachUser?.FirstName))
                            {
                                coachesNames.Add((coachUser.FirstName + " " + coachUser.LastName).Trim());
                            }
                            foreach (string coachesName in coachesNames)
							{
                                foreach(string participantName in currentParticipantsNames)
								{
                                    if(participantName == coachesName)
									{
                                        return OperationResult.Success(string.Empty);
                                    }
								}
							}
						}
                    }

                }

                room = await RoomResource.UpdateAsync(new UpdateRoomOptions(
                    viewModel.RoomId,
                    RoomResource.RoomStatusEnum.Completed));
            }
            catch (ApiException ex)
            {
                return OperationResult.Failure(
                    "Unable to delete room due to video vendor error. Please contact support and provide this details:" +
                    $"vendor error code: {ex.Code}; " +
                    $"vendor additional error info: {ex.MoreInfo}; " +
                    $"UTC time of attempt {DateTime.UtcNow:MM/dd/yyyy HH:mm:ss}; ");
            }
            catch (TwilioException)
            {
                return OperationResult.Failure(
                    $"Unable to create room due to video vendor error. Please contact support and provide this UTC time for your deletion attempt {DateTime.UtcNow:MM/dd/yyyy HH:mm:ss}");
            }

            if (room != null && room.Status == RoomResource.RoomStatusEnum.Completed)
            {
                // update videoRoomInfo status

                var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);

                OperationResult setRoomClosedResult = contributionVm.SetRoomAsClosedById(viewModel.RoomId);

                if (setRoomClosedResult.Failed)
                {
                    _logger.Log(LogLevel.Information, $"Failed to mark roomId: {viewModel.RoomId} as not running");
                }
                else
                {
                    var contributionUpdated = _mapper.Map<ContributionBase>(contributionVm);
                    _logger.Log(LogLevel.Information, $"Before save. ContributionVm: {JsonSerializer.Serialize(contributionUpdated)}");

                    var contributionResult = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                        .Update(contributionUpdated.Id, contributionUpdated);
                }

                return OperationResult.Success(string.Empty);
            }

            return OperationResult.Failure(
                "Unable to delete room. Please try again and if the problem persists contact support");
        }

        public async Task<OperationResult> HandleRoomDeletionVendorConfirmation(string contributionId, string classId)
        {
            try
            {
                var contribution = await _contributionRootService.GetOne(contributionId);

                if (contribution is null)
                {
                    return OperationResult.Failure("Unable to find contribution to update video room info");
                }

                var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);

                _logger.Log(LogLevel.Information, $"Before SetRoomAsClosedById is 'room-ended'. ContributionId: {contributionId}");

                _logger.Log(LogLevel.Information, $"{JsonSerializer.Serialize(contributionVm)}");

                OperationResult setRoomClosedResult = contributionVm.SetRoomAsClosedById(classId);

                _logger.Log(LogLevel.Information, $"After SetRoomAsClosedById is 'room-ended'. ContributionVm: {JsonSerializer.Serialize(contributionVm)}");

                if (!setRoomClosedResult.Failed)
                {
                    return OperationResult.Failure(setRoomClosedResult.Message);
                }

                var contributionUpdated = _mapper.Map<ContributionBase>(contributionVm);
                _logger.Log(LogLevel.Information, $"Before save. ContributionVm: {JsonSerializer.Serialize(contributionUpdated)}");

                var contributionResult = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                    .Update(contributionUpdated.Id, contributionUpdated);


                if (contributionResult != null)
                {
                    return OperationResult.Success($"Room info with id {classId} marked as deleted");
                }

                return OperationResult.Failure(setRoomClosedResult.Message);
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, $"HandleRoomDeletionVendorConfirmation Exception: {e}");
                throw;
            }
        }

        public async Task NotifyVideoRetrievalService(string compositionId, string roomSid, DateTime timeOfRecording)
        {
            var contributionId = await _contributionService.GetContributionIdByRoomId(roomSid);

            if (contributionId == null)
            {
                throw new Exception($"not able to find contribution for room Sid:{roomSid}");
            }

            var request = new VideoRetrievalMessage
            {
                RoomId = roomSid,
                ContributionId = contributionId,
                CompositionId = compositionId,
                TimeOfRecording = timeOfRecording
            };

            await _amazonSQS.SendMessageAsync(_sqsQueueUrl, JsonSerializer.Serialize(request));
        }

        public async Task<string> GetPresignedUrl(string accountId, string roomId, string contributionId, bool allowAnonymous = false)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution is null)
            {
                throw new Exception($"contribution with id {contributionId} not found");
            }

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            if (!contributionVm.IsRoomRecorded(roomId))
            {
                throw new Exception(
                    $"Room with Id {roomId} has no recordings or not belongs to contribution with id {contributionId}");
            }

            //need verification if anonyous is not allowed
            if (!allowAnonymous)
            {
                await FillPodsForSessionContribution(contributionVm);
                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
                var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                        .GetOne(p => p.ClientId == user.Id && p.ContributionId == contributionId);
                var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);

                if (contributionVm is SessionBasedContributionViewModel vm)
                {
                    if (!vm.IsOwnerOrPartner(user.Id))
                    {
                        try
                        {
                            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
                            purchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
                        }
                        catch { }
                        if (purchaseVm == null || (!purchaseVm.HasAccessToContribution && !purchaseVm.IsPaidAsSubscription && !purchaseVm.IsPaidAsEntireCourse))
                        {
                            if (!await _accountManager.IsAdminOrSuperAdmin(accountId))
                            {
                                throw new AccessDeniedException($"User is not owner or participant");
                            }
                        }
                    }
                }
                else if (!contributionVm.IsOwnerOrPartnerOrParticipant(user.Id, roomId))
                {
                    if (!await _accountManager.IsAdminOrSuperAdmin(accountId))
                    {
                        throw new AccessDeniedException($"User is not owner or participant");
                    }
                } 
            }

            var info = contributionVm.GetRecordingInfo(roomId);
            if (info is null || info.Status != RecordingStatus.Available)
            {
                throw new InvalidOperationException($"Recordings still not available");
            }

            return string.IsNullOrEmpty(_distributionName) 
                ? GeneratePresignedUrl(roomId, info.CompositionFileName)
                : $"{_distributionName}/Videos/Rooms/{roomId}/Compositions/{info.CompositionFileName}";
        }

        public async Task<OperationResult> GetRoomStatus(string accountId, string contributionId, string classId)
        {
            var contributionVm =
                _mapper.Map<ContributionBaseViewModel>(await _contributionRootService.GetOne(contributionId));
            if (contributionVm != null)
            {
                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

                if (user == null)
                {
                    return OperationResult.Failure("user not found");
                }

                if (!contributionVm.IsOwnerOrPartner(user.Id))
                {
                    return OperationResult.Forbid("Not owner or partner");
                }

                if (contributionVm is SessionBasedContributionViewModel sessionBasedContributionVm)
                {
                    var session =
                        sessionBasedContributionVm.Sessions.FirstOrDefault(s =>
                            s.SessionTimes.Any(st => st.Id == classId));
                    if (session == null)
                    {
                        return OperationResult.Failure("class not found");
                    }

                    var sessionTime = session.SessionTimes.FirstOrDefault(st => st.Id == classId);
                    return await CheckRoomStatusAsync(sessionTime.VideoRoomInfo);
                }
                else if (contributionVm is ContributionOneToOneViewModel contributionOneToOneVm)
                {
                    var session =
                        contributionOneToOneVm.AvailabilityTimes.FirstOrDefault(s =>
                            s.BookedTimes.Any(bt => bt.Id == classId));
                    if (session == null)
                    {
                        return OperationResult.Failure("class not found");
                    }

                    var sessionTime = session.BookedTimes.FirstOrDefault(st => st.Id == classId);
                    return await CheckRoomStatusAsync(sessionTime.VideoRoomInfo);
                }
                else
                {
                    return OperationResult.Failure("contribution type not supported yet");
                }
            }
            else
            {
                return OperationResult.Failure("contribution not found");
            }
        }

        public async Task<OperationResult> GetPresignedUrlForRecordedSession(string accountId, string contributionId, string sessionId, string sessionTimeId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(p => p.ClientId == user.Id && p.ContributionId == contributionId);

            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution is null)
            {
                return OperationResult.Failure($"contribution with id {contributionId} not found");
            }
            var isPartner = contribution.Partners?.Any(x => x.UserId == user.Id) ?? false;

            if (purchase is null && contribution.UserId != user.Id && !isPartner && !await _accountManager.IsAdminOrSuperAdmin(accountId))
            {
                return OperationResult.Failure("User is not owner or participant");
            }

            var session = new Session();

            if (contribution is SessionBasedContribution contributionCourse)
            {
                session = contributionCourse.Sessions.FirstOrDefault(x => x.Id == sessionId);
            }

            var presignedUrlVm = new PresignedUrlViewModel();

            if (!string.IsNullOrEmpty(sessionTimeId))
            {
                var sessionTime = session?.SessionTimes.FirstOrDefault(x => x.Id == sessionTimeId);
                presignedUrlVm.PresignedUrl = GeneratePresignedUrlForRecordedSession(sessionTime?.PrerecordedSession?.DocumentKeyWithExtension, sessionTime.SubCategoryName, sessionTime?.PrerecordedSession?.Extension);
                presignedUrlVm.DateAvailable = sessionTime.IgnoreDateAvailable ? null : sessionTime?.StartTime;
                presignedUrlVm.Duration = sessionTime?.PrerecordedSession?.Duration;

                return OperationResult.Success("Operation successfull", presignedUrlVm);
            }
            var validationAnswer = ValidatorForSession(session, user.Id == contribution.UserId);

            presignedUrlVm.PresignedUrl = GeneratePresignedUrlForRecordedSession(session?.PrerecordedSession?.DocumentKeyWithExtension);
            presignedUrlVm.DateAvailable = session?.DateAvailable;
            presignedUrlVm.Duration = session?.PrerecordedSession?.Duration;

            if (validationAnswer != string.Empty)
            {
                return OperationResult.Failure(validationAnswer);
            }

            return OperationResult.Success("Operation successfull", presignedUrlVm);
        }


        public string GetVideoUrl(string videoKey) 
        {

            return GeneratePresignedUrlForRecordedSession(videoKey);
        }

        private async Task<OperationResult> CheckRoomStatusAsync(VideoRoomInfo videoRoomInfo)
        {
            if (videoRoomInfo == null)
            {
                return OperationResult.Success(string.Empty, new RoomStatusViewModel()
                {
                    CreatorId = null,
                    isRunning = false
                });
            }

            TwilioClient.Init(_accountSid, _authToken);

            var roomResource = await RoomResource.FetchAsync(new FetchRoomOptions(videoRoomInfo.RoomId));
            if (roomResource.Status == RoomResource.RoomStatusEnum.InProgress)
            {
                return OperationResult.Success(string.Empty, new RoomStatusViewModel()
                {
                    CreatorId = videoRoomInfo.CreatorId,
                    isRunning = roomResource.Status == RoomResource.RoomStatusEnum.InProgress
                });
            }

            return OperationResult.Success(string.Empty, new RoomStatusViewModel()
            {
                CreatorId = null,
                isRunning = false
            });
        }

        private async Task<CompositionResource> CreateCompositionForRoom(string contributionId, string roomSid)
        {
            var contributionWebHookUrl = BuildContributionWebHookUrl(contributionId);

            return await CreateAsync(
                roomSid,
                videoLayout: _defaultLayout,
                audioSources: new List<string>() {"*"},
                resolution: "1280x720",
                statusCallback: new Uri(contributionWebHookUrl),
                statusCallbackMethod: HttpMethod.Post,
                format: FormatEnum.Mp4);
        }

        private async Task<string> GetUserIdentity(
            string userId,
            ContributionBaseViewModel contribution,
            string classId)
        {
            var allIdentities = contribution.GetAllIdentitiesInClass(classId);
            return await GetUniqueUserIdentity(userId, allIdentities);
        }

        private async Task<string> GetUniqueUserIdentity(string userId, IEnumerable<string> userIds)
        {
            var allUsers = await _unitOfWork.GetRepositoryAsync<User>().Get(e => userIds.Contains(e.Id));

            Dictionary<string, string> allUserIdentities = GetUniqueIdentities(allUsers);

            return allUserIdentities[userId];
        }

        private Dictionary<string, string> GetUniqueIdentities(IEnumerable<User> allUsers)
        {
            var allUserIdentities =
                allUsers.ToDictionary(key => key.Id, value => $"{value.FirstName} {value.LastName}");

            if (allUserIdentities.Values.Distinct().Count() != allUserIdentities.Values.Count)
            {
                var grouped = allUserIdentities.GroupBy(e => e.Value);
                var notUniqueGroups = grouped.Where(e => e.Count() > 1);

                foreach (var notUniqueGroup in notUniqueGroups)
                {
                    foreach (var notUniqueItem in notUniqueGroup.OrderBy(e => e.Key).Select((value, i) => (value, i)))
                    {
                        allUserIdentities[notUniqueItem.value.Key] += $" ({(notUniqueItem.i + 1).ToString()})";
                    }
                }
            }

            return allUserIdentities;
        }

        private string GeneratePresignedUrl(string roomId, string compositionFileName)
        {
            var duration = TimeSpan.FromDays(1);

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = $"Videos/Rooms/{roomId}/Compositions/{compositionFileName}",
                Expires = DateTime.UtcNow.Add(duration)
            };

            return _amazonS3.GetPreSignedURL(request);
        }

        private string GeneratePresignedUrlForRecordedSession(string prerecordedSessionKeyWithExtension, string subCategoryName = null, string extension = null)
        {
            var duration = TimeSpan.FromDays(1);

            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = $"{prerecordedSessionKeyWithExtension}",
                Expires = DateTime.UtcNow.Add(duration)
            };

            if (!string.IsNullOrEmpty(subCategoryName))
            {
                request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename={Regex.Replace(subCategoryName, "[^a - zA - Z\\d\\s:]", "")}{extension}";
            }

            return _amazonS3.GetPreSignedURL(request);
        }

        private string ValidatorForSession(Session session, bool isContributor)
        {
            if (session is null)
            {
                return $"session with id {session?.Id} not found";
            }

            if (!session.IsPrerecorded || session.PrerecordedSession is null)
            {
                return $"session with id {session.Id} don't have recorded session";
            }

            if (session.DateAvailable > DateTime.Now && !isContributor)
            {
                return $"video is unavailable";
            }

            return string.Empty;
        }

        private async Task FillPodsForSessionContribution(ContributionBaseViewModel contributionVm)
        {
            if (contributionVm is SessionBasedContributionViewModel vm)
            {
                var podIds = vm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                vm.Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
            }
        }
    }
}