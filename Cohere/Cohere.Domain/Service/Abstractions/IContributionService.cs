using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.ForAdmin;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Entity.Enums.Contribution;
using Microsoft.AspNetCore.Http;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContributionService
    {
        Task<OperationResult> Insert(ContributionBaseViewModel viewModel, string creatorAccountId);

        Task<OperationResult> Update(ContributionBaseViewModel viewModel, string requesterAccountId);

        Task<OperationResult> Delete(string id);

        Task<JourneyClassesAllViewModel> GetForClientJourneyAsync(string userId);
        Task<JourneyClassesAllViewModelUpdated> GetBoughtByUserIdUpdated(string userId);

        Task<OperationResult> GetForAllClientPurchasesAsync(string requestorAccountId, string userId);

        Task<OperationResult> GetUpcomingCreatedByCohealerAsync(string userId, string fromJwtAccountId, int? skip, int? take, OrderByEnum orderByEnum);

        Task<OperationResult<IEnumerable<string>>> GetCohealerContributionIds(string coachAccountId);

        Task<OperationResult<IEnumerable<string>>> GetPartnerContributionIds(string partnerAccountId);

        Task<OperationResult> GetUpcomingCreatedByCohealerAsync(string userId, string fromJwtAccountId, string contributionType);

        Task<OperationResult> GetArchivedCreatedByCohealerAsync(string userId, string fromJwtAccountId);

        Task<OperationResult> GetCohealerContributionByIdAsync(string id, string accountId);

        Task<ContributionBaseViewModel> GetClientContributionByIdAsync(string id, string accountId = null);

        Task<OperationResult> ChangeStatusAsync(string contributionId, string adminAccoutId, string accountId, AdminReviewNoteViewModel model);

        Task<OperationResult> ShareContribution(ShareContributionEmailViewModel shareContributionVm, string inviterAccountId);

        Task<GroupedAdminContributionsViewModel> GetAllContributionsForAdminAsync(string adminAccountId);

        Task<DashboardContributionsViewModel> GetDashboardContributionsForCohealerAsync(string accountId);
        Task<DashboardContributionsViewModel> GetAllSessionsForCohealer(string accountId, bool isPartner, int? skip, int? take, string type = null, List<ContributionStatuses> contributionStatuses = null);
        Task<bool> IsContributionPurchasedByUser(string contributionId, string userId);

        Task<OperationResult> AssignRoomIdAndNameToClass(ContributionBaseViewModel contributionVm, VideoRoomInfo videoRoomInfo, string classId);

        Task<CohealerInfoViewModel> GetCohealerInfoForClient(string cohealerUserId, string requestorAccountId);

        Task<OperationResult> SetContributionClassAsCompleted(SetClassAsCompletedViewModel viewModel, string requestorAccountId);

        Task<OperationResult> SetContributionSelfPacedClassAsCompleted(SetClassAsCompletedViewModel viewModel, string requestorAccountId);

        Task<OperationResult> SetContributionAsCompletedAsync(SetAsCompletedViewModel viewModel, string requestorAccountId);

        Task<OperationResult> AddAttachmentToContribution(ContributionBase contribution, string sessionId, Document document);

        Task<OperationResult> RemoveAttachmentFromContribution(ContributionBase contribution, string sessionId, string documentId);
        Task<OperationResult> RemoveAttachmentFromContributionSessionTimes(ContributionBase contribution, string documentId, bool isVideo);

        OperationResult GetAttachmentFromContribution(ContributionBase contribution, string sessionId, string documentId);
        OperationResult GetAttachmentFromContributionSelfPacedSessions(ContributionBase contribution, string documentId);

        Task<OperationResult<string>> CreatePartnerCoachAssignRequest(string contributionId, string contributionOwnerId, string partnerEmail);

        Task<OperationResult<ContributionBaseViewModel>> AssignPartnerCoachToContribution(string contributionId, string contributionOwnerUserId, string assignCode);

        Task<OperationResult<GroupedTableContributionViewModel>> GetPartnerContributions(string accountId, string type = null, List<ContributionStatuses> contributionStatuses = null, bool fromDashboard = false);
        
        Task<OperationResult> DeletePartnerFromContribution(string contributionId, string partnerUserId, string requsetorAccountId);

        Task<OperationResult<List<ContributionPartnerViewModel>>> GetContributionPartnersAsync(string contributionId);

        Task<OperationResult<ContributionOneToOneViewModel>> RescheduleOneToOneCoachBooking(string coachAccountId, string contributionId, string rescheduleFromId, string rescheduleToId, string note, int sessionOffsetInMinutes);

        Task<OperationResult<ContributionOneToOneViewModel>> RescheduleOneToOneClientBooking(string clientAccountId, string contributionId, string rescheduleFromId, string rescheduleToId, string note, int sessionOffsetInMinutes);

        Task<IEnumerable<(string ContributionId, DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase)>> GetClientContributionIdsWithDeclinedSubscription(string accountId);

        Task<IEnumerable<FailedSubscription>> ListClientIncompleteSubscription(string clientAccountId);

        Task<IEnumerable<FailedSubscription>> ListCoachIncompleteSubscriptions(string coachAccountId);

        Task<OperationResult<string>> CreateCheckoutSession(string clientAccountId);

        Task<ContributionMetadataViewModel> GetContributionMetadata(string contributionId);

        Task<ContributionMetadataViewModel> GetWebsiteLinkMetadata(string coachName);

        Task<ContributionBase> Get(string contributionId);

        Task<string> GetContributionIdByRoomId(string roomSid);

        Task<OperationResult> DeleteUnfinished(string contributionId, string requestorAccountId);
        Task<OperationResult> DeleteContribution(string contributionId, string requestorAccountId);

        Task<OperationResult<ContributionBaseViewModel>> UpdateUnfinished(ContributionBaseViewModel viewModel, string creatorAccountId);

        Task<OperationResult<ContributionBaseViewModel>> InsertUnfinished(ContributionBaseViewModel viewModel, string creatorAccountId);

        Task<OperationResult<ContributionBaseViewModel>> SubmitUnfinished(string contributionId, string accountId);

        Task<OperationResult<ContributionBaseViewModel>> UseAsTemplate(string contributionId, string accountId);

        Task<OperationResult<ContributionBaseViewModel>> GetLastUnfinishedAsync(string accountId);

        Task<IEnumerable<ContributionBaseViewModel>> GetClientContributionByType(string accountId, string type);

        Task<List<ParticipantViewModel>> GetParticipantsVmsAsync(string contributionId, User user = null);

        Task UserViewedRecording(UserViewedRecordingViewModel model);
        Task<OperationResult> SaveSignoffInfo(SignoffInfoViewModel model, IFormFile file, string accountId);
        Task<OperationResult> AddOrUpdateCustomTemplate(EmailTemplatesViewModel emailTemplatesviewModel, string accountId);
        Task<EmailTemplates> GetCustomTemplateByContributionId(string contributionId);
        Task<OperationResult> EnableEmailTemplate(string accountId, string contributionId, string emailType, bool IsEnabled);
        Task<OperationResult> SetDefaultEmailTemplatesData(string accountId, string contributionId);
        Task<OperationResult> UpdateEmailTemplate(string contributionId, CustomTemplate Template);
        Task<OperationResult> CopyContributionEmailSettings(string FromContributionId, string ToContributionId);
        Task<OperationResult> GetCustomizedContributions(string accountId, string contributionId);
        Task<OperationResult> GetCoachContributionsForZapier(string accountId);
        void GetActiveCampaignResult(string accountId);
        Task<IEnumerable<SelfpacedDetails>> DownloadSelfpacedModuleDetails(string contributionId, string accountId);
        Task<OperationResult> EnableBrandingColorsOnEmailTemplates(string accountId, string contributionId, bool IsEnabled);
    }
}
