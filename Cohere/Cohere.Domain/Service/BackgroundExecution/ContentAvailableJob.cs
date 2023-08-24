using System;
using System.Linq;
using Cohere.Domain.Extensions;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.FCM;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class ContentAvailableJob : IContentAvailableJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFCMService _fcmService;
        private readonly ILogger<SendEmailCoachInstructionGuideJob> _logger;

        public ContentAvailableJob(IUnitOfWork unitOfWork, ILogger<SendEmailCoachInstructionGuideJob> logger, IFCMService fcmService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _fcmService = fcmService;
        }

        public void Execute(params object[] args)
        {
            var jobGuid = Guid.NewGuid();
            _logger.LogInformation($"Started Job {nameof(ContentAvailableJob)}. Job Guid: {jobGuid}");
            try
            {
                if (!args.Any())
                {
                    throw new ArgumentException("Args has no elements");
                }
                var sessionId = args[0] as string;
                var sessionTimeId = args[1] as string;
                var contributionId = args[2] as string;
                var requestUserId = args[3] as string;
                if (string.IsNullOrWhiteSpace(sessionTimeId))
                {
                    throw new ArgumentException("sessionId is not provided");
                }

                var contributionRepo = _unitOfWork.GetGenericRepositoryAsync<ContributionBase>();
                var contributionTask = contributionRepo.GetOne(x => x.Id == contributionId);
                contributionTask.Wait();
                var contribution = contributionTask.Result;

                if (contribution is null)
                {
                    throw new ArgumentException("Contribution not found");
                }
                if(contribution is SessionBasedContribution sessionBasedContribution)
                {
                    var session = sessionBasedContribution.GetSessionBySessionTimeId(sessionTimeId);
                    if (session != null)
                    {
                        try
                        {
                            _fcmService.SendSelfPacedContentAvailablePushNotification(sessionId, sessionTimeId, contributionId, requestUserId);
                            var sessiontime = session.SessionTimes.Where(x => x.Id == sessionTimeId).FirstOrDefault();
                            sessiontime.ScheduledNotficationJobId = null;
                            _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contribution);
                        }
                        catch
                        {

                        }
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
