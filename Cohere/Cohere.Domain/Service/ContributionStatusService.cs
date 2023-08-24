using System;
using System.Threading.Tasks;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Service
{
    public class ContributionStatusService : IContributionStatusService {
        private readonly IContributionRootService _contributionRootService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ContributionStatusService> _logger;

        public ContributionStatusService(IContributionRootService contributionRootService, IUnitOfWork unitOfWork, INotificationService notificationService, ILogger<ContributionStatusService> logger)
        {
            _contributionRootService = contributionRootService;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }
        
        public async Task ExposeContributionsToReviewAsync(string userId)
        {
            var inSandboxList = await _contributionRootService.Get(c => c.UserId == userId && c.Status == ContributionStatuses.InSandbox);

            foreach (var contribution in inSandboxList)
            {
                contribution.Status = ContributionStatuses.InReview;
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

                try
                {
                    await _notificationService.SendContributionStatusNotificationToAuthor(contribution);
                    await _notificationService.SendEmailAboutInReviewToAdmins(contribution);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during sending status notification email");
                }
            }
        }
    }
}