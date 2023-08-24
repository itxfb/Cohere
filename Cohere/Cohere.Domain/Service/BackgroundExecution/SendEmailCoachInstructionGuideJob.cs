using System;
using System.Linq;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class SendEmailCoachInstructionGuideJob : ISendEmailCoachInstructionGuideJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SendEmailCoachInstructionGuideJob> _logger;
        private readonly INotificationService _notificationService;

        public SendEmailCoachInstructionGuideJob(IUnitOfWork unitOfWork, ILogger<SendEmailCoachInstructionGuideJob> logger, INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Send Email To all Coaches with instructions how to create contribution
        /// </summary>
        /// <param name="args">
        /// 1) args[0] string - UserId
        /// </param>
        public void Execute(params object[] args)
        {
            var jobGuid = Guid.NewGuid();
            _logger.LogInformation($"Started Job {nameof(SendEmailCoachInstructionGuideJob)}. Job Guid: {jobGuid}");
            try
            {
                if (!args.Any())
                {
                    throw new ArgumentException("Args has no elements");
                }

                var userId = args[0] as string;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("UserId is not provided");
                }

                var userRepo = _unitOfWork.GetRepositoryAsync<User>();
                var userTask = userRepo.GetOne(x => x.Id == userId);
                userTask.Wait();
                var user = userTask.Result;

                if (user is null)
                {
                    throw new ArgumentException("User not found");
                }

                var accountRepo = _unitOfWork.GetRepositoryAsync<Account>();
                var accountTask = accountRepo.GetOne(x => x.Id == user.AccountId);
                accountTask.Wait();
                var account = accountTask.Result;

                if (account == null)
                {
                    throw new ArgumentException("User not found");
                }

                // handled in active campaign
                //_logger.LogInformation($"Call NotificationService.SendEmailCohealerInstructionGuide({account.Email},{user.FirstName}). Job Guid: {jobGuid}");

                //_notificationService.SendEmailCohealerInstructionGuide(account.Email, user.FirstName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
