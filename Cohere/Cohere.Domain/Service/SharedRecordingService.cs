using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Video;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class SharedRecordingService : ISharedRecordingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IVideoService _videoService;

        public SharedRecordingService(IUnitOfWork unitOfWork, IVideoService videoService)
        {
            _unitOfWork = unitOfWork;
            _videoService = videoService;
        }

        public async Task<OperationResult> InsertInfoToShareRecording(string contributionId, string sessionTimeId, string accountId)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
            if(contribution is null)
            {
                return OperationResult.Failure("No such contribution exist with this ID");
            }
            //By default currently all recordings are avialble to make them public
            //if (!contribution.IsRecordingPublic)
            //{
            //    return OperationResult.Failure("Recordings are not public for the contribution.");
            //}

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (coachUser.Id != contribution.UserId)
            {
                return OperationResult.Failure("not allowed to access other coach's contribution");
            }

            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                var sessionTime = sessionBasedContribution.Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == sessionTimeId);
                if (string.IsNullOrWhiteSpace(sessionTime.PassCode))
                {
                    sessionTime.PassCode = Guid.NewGuid().ToString("d").Substring(0, 6);
                    await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(sessionBasedContribution.Id, sessionBasedContribution);
                }
                return OperationResult.Success("PassCode for recording has been for session time", new SharedRecordingViewModel
                {
                    ContributionId = contribution.Id,
                    SessionTimeId = sessionTimeId,
                    PassCode = sessionTime.PassCode,
                    IsPassCodeEnabled = sessionTime.IsPassCodeEnabled
                });
            }

            return OperationResult.Failure("contribution is not session based");
        }

        public async Task<OperationResult> ChangePassCodeStatus(string contributionId, string sessionTimeId, string accountId, bool isPassCodeEnabled)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
            if (contribution is null)
            {
                return OperationResult.Failure("No such contribution exist with this ID");
            }
            //By default currently all recordings are avialble to make them public
            //if (!contribution.IsRecordingPublic)
            //{
            //    return OperationResult.Failure("Recordings are not public for the contribution.");
            //}

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (coachUser.Id != contribution.UserId)
            {
                return OperationResult.Failure("not allowed to access other coach's contribution");
            }

            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                var sessionTime = sessionBasedContribution.Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == sessionTimeId);
                sessionTime.IsPassCodeEnabled = isPassCodeEnabled;
                await _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().Update(sessionBasedContribution.Id, sessionBasedContribution);

                return OperationResult.Success();
            }

            return OperationResult.Failure("contribution is not session based");
        }

        public async Task<OperationResult<List<RecordingInfo>>> GetSharedRecordingsInfo(string contributionId, string sessionTimeId, string passCode = null)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
            if (contribution is null)
            {
                return OperationResult<List<RecordingInfo>>.Failure("No such contribution exist with this ID");
            }
            //By default currently all recordings are avialble to make them public
            //if (!contribution.IsRecordingPublic)
            //{
            //    return OperationResult<List<RecordingInfo>>.Failure("Recordings are not public for the contribution.");
            //}

            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                var sessionTime = sessionBasedContribution.Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == sessionTimeId);
                
                // if passCode optional or valid
                if(!sessionTime.IsPassCodeEnabled || (!string.IsNullOrEmpty(passCode) && sessionTime.PassCode == passCode))
                {
                    return OperationResult<List<RecordingInfo>>.Success(null, sessionTime.RecordingInfos);
                }

                //need correct passcode from the user
                return OperationResult<List<RecordingInfo>>.Failure("Correct PassCode is requeire to get the required recording information");
            }

            return OperationResult<List<RecordingInfo>>.Failure("contribution is not session based");
        }

        public async Task<OperationResult<string>> GetSharedRecordingPresignedUrl(string contributionId, string sessionTimeId, string roomId, string passCode = null)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
            if (contribution is null)
            {
                return OperationResult<string>.Failure("No such contribution exist with this ID");
            }
            //By default currently all recordings are avialble to make them public
            //if (!contribution.IsRecordingPublic)
            //{
            //    return OperationResult<string>.Failure("Recordings are not public for the contribution.");
            //}

            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                var sessionTime = sessionBasedContribution.Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == sessionTimeId);

                // if passCode optional or valid
                if (!sessionTime.IsPassCodeEnabled || (!string.IsNullOrEmpty(passCode) && sessionTime.PassCode == passCode))
                {
                    var presignedUrl = await  _videoService.GetPresignedUrl(accountId:null, roomId, contributionId, allowAnonymous:true); //accountId won't need when it is anonymous request
                    return OperationResult<string>.Success(null, presignedUrl);
                }

                //need correct passcode from the user
                return OperationResult<string>.Failure("Correct PassCode is requeire to get the required recording information");
            }

            return OperationResult<string>.Failure("contribution is not session based");
        }
    }
}
