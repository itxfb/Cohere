using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Note;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.User;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;

using FluentValidation;
using FluentValidation.Results;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    [Newtonsoft.Json.JsonConverter(typeof(ContributionJsonConverter))]
    public abstract class ContributionBaseViewModel : BaseDomain
    {
        public ContributionBaseViewModel(IValidator validator)
        {
            Validator = validator;
        }

        public string UserId { get; set; }

        public string Title { get; set; }

        public string ServiceProviderName { get; set; }

        public string Status { get; set; }

        public string TimeZoneId { get; set; }

        public List<string> Categories { get; set; }

        public string Type { get; set; }

        public string StripeAccount { get; set; }

        public List<string> PreviewContentUrls { get; set; }

        private string tagline;
        public string ContributionTagline
        {
            get { return tagline; }
            set
            {
                if (!string.IsNullOrEmpty(value) && value.Length > 200)
                {
                    tagline = value.Substring(0, 199);
                }
                else
                {
                    tagline = value;
                }
            }
        }
        public string Purpose { get; set; }

        public string WhoIAm { get; set; }

        public string WhatWeDo { get; set; }
        public bool IsDownloadEnabled { get; set; }
        public string Preparation { get; set; }

        public string WhoIAmLabel { get; set; }

        public string WhatWeDoLabel { get; set; }
        public bool IsElectronicSignatureActive { get; set; }
        public string WhoIAmIcon { get; set; }
        public string WhatWeDoIcon { get; set; }
        public string PurposeIcon { get; set; }
        public string PreparationIcon { get; set; }
        public string PurposeLabel { get; set; }
        public bool IsWorkshop { get; set; }
        public string PreparationLabel { get; set; }
        public bool ViewPurpose { get; set; }

        public bool ViewWhoIAm { get; set; }

        public bool ViewWhatWeDo { get; set; } 

        public bool ViewPreparation { get; set; }

        public bool IsCustomBrandingColorsActive { get; set; }
        public bool IsLegacyColorsActive { get; set; }
        public bool IsDarkModeEnabled { get; set; }
        public bool IsCoachSelectedDarkModeEnabled { get; set; }
        public Dictionary<string, string> BrandingColors { get; set; }
        public Dictionary<string, string> CoachSelectedBrandingColors { get; set; }
        public string ActiveTemplate { get; set; }
        public string CustomToS { get; set; }
        public string CustomLogo { get; set; }

        public string InstagramUrl { get; set; }

        public string LinkedInUrl { get; set; }

        public string YoutubeUrl { get; set; }
        public bool IsMembersHiddenInCommunity { get; set; }
        public bool IsMembersHiddenInGroupChat { get; set; }
        public bool IsGroupChatHidden { get; set; }
        public bool IsLiveSessionMembersHidden { get; set; }
        public string FacebookUrl { get; set; }

        public string TiktokUrl { get; set; }

        public string WebsiteUrl { get; set; }

        public string ThankYouPageRedirectUrl { get; set; }

        public List<string> LanguageCodes { get; set; }

        public bool IsSpeakingLanguageRequired { get; set; }

        public string MinAge { get; set; }

        public string Gender { get; set; }

        public PaymentInfoViewModel PaymentInfo { get; set; }
        public string PaymentType { get; set; }
        public string TaxType { get; set; }
        public bool IsInvoiced { get; set; }
        public string Bio { get; set; }
        public string CoachCountry { get; set; }
        public bool ArePublicPostsAllowed { get; set; }
        public bool IsCommunityFeatureHiddden { get; set; } = false;
        public bool EnableCommunityPosts { get; set; } = true;
        public bool AreClientPublicPostsAllowed { get; set; }
        public bool InvitationOnly { get; set; }
        public long UnreadPostCount { get; set; }
        public string LatestDraftPostId { get; set; }
        public float Rating { get; set; }
        public string CustomTitleForMeetYourCoach { get; set; }
        public int LikesNumber { get; set; }
        public int PercentageCompleted { get; set; }
        public string DefaultCurrency { get; set; }
        public string DefaultSymbol { get; set; }
        public string ExternalCalendarEmail { get; set; }

        public List<Currency> AvailableCurrencies { get; set; }

        public List<Review> Reviews { get; set; }

        protected TimeRange TimePeriod;

        protected List<TimeRange> BusyTimeRanges;

        protected IValidator Validator;

        public abstract List<TimeRange> CohealerBusyTimeRanges { get; set; }

        public string PurchaseStatus { get; set; } = "unpurchased";

        public bool IsPurchased { get; set; }

        public bool IsAccessRevokedByCoach { get; set; }

        public bool HasAgreedContributionTerms { get; set; }

        public List<AdminReviewNote> AdminReviewNotes { get; set; } = new List<AdminReviewNote>();

        public List<ParticipantViewModel> Participants { get; set; } = new List<ParticipantViewModel>();
        public List<Cohere.Entity.Entities.Testimonial> testimonials { get; set; }

        public GroupChat Chat { get; set; }

        public decimal EarnedRevenue { get; set; }

        public List<ContributionPartner> Partners { get; set; } = new List<ContributionPartner>();

        public ClosestClassForBannerViewModel ClosestClassForBanner { get; set; }

        public LiveVideoProvider LiveVideoServiceProvider { get; set; } = new LiveVideoProvider { ProviderName = Constants.LiveVideoProviders.Cohere };

        public string CustomInvitationBody { get; set; }
        
        public abstract ClosestClassForBannerViewModel GetClosestCohealerClassForBanner(string coachTimeZoneId);

        public abstract ClosestClassForBannerViewModel GetClosestClientClassForBanner(string clientId, string coachTimeZoneId);

        public IEnumerable<NoteViewModel> Notes { get; set; }

        public List<long> ZoomMeetigsIds { get; set; }

        public abstract bool ArchivingAllowed { get; }
        public bool DeletingAllowed { get; set; }
        public virtual void ConvertAllOwnZonedTimesToUtc(string timeZoneId)
        {
            if (TimePeriod != null)
            {
                TimePeriod.StartTime = DateTimeHelper.GetUtcTimeFromZoned(TimePeriod.StartTime, timeZoneId);
                TimePeriod.EndTime = DateTimeHelper.GetUtcTimeFromZoned(TimePeriod.EndTime, timeZoneId);
            }

            CohealerBusyTimeRanges.ForEach(tr =>
            {
                tr.StartTime = DateTimeHelper.GetUtcTimeFromZoned(tr.StartTime, timeZoneId);
                tr.EndTime = DateTimeHelper.GetUtcTimeFromZoned(tr.EndTime, timeZoneId);
            });

            TimeZoneId = string.Empty;
        }

        public virtual void ConvertAllOwnUtcTimesToZoned(string timeZoneId)
        {
            if (TimePeriod != null)
            {
                TimePeriod.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(TimePeriod.StartTime, timeZoneId);
                TimePeriod.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(TimePeriod.EndTime, timeZoneId);
            }

            CohealerBusyTimeRanges.ForEach(tr =>
            {
                tr.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(tr.StartTime, timeZoneId);
                tr.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(tr.EndTime, timeZoneId);
            });

            TimeZoneId = timeZoneId;
        }

        public abstract void AssignIdsToTimeRanges();

        public abstract OperationResult RevokeAssignmentUserToContributionTime(BookTimeBaseViewModel bookModel, UserViewModel user);

        public List<string> GetBookedParticipantsIds()
        {
            return ClassesInfo.Values.SelectMany(e => e.ParticipantIds).Distinct().ToList();
        }
       
        public virtual void ClearHiddenForClientInfo(string clientUserId = null, PurchaseViewModel purchaseVm = null)
        {
            AdminReviewNotes.Clear();

            if (Chat != null && Chat.CohealerPeerChatSids.Any())
            {
                Chat.CohealerPeerChatSids.Clear();
            }

            PaymentInfo.BillingPlanInfo = null;
            if (PaymentInfo.MembershipInfo?.ProductBillingPlans != null)
            {
                PaymentInfo.MembershipInfo.ProductBillingPlans = null;
            }
        }

        public abstract void AssignChatSidForUserContributionPage(string clientUserId);

        public async Task<ValidationResult> ValidateAsync()
        {
            return await Validator.ValidateAsync(this);
        }

        public abstract JourneyClassesInfosAll GetClassesInfosForParticipant(string participantId, string timeZoneId, string shortName);

        public int GetNumberOfCompletedClassesForParticipant(string participantId)
        {
            return ClassesInfo.Values
                .Count(st => st.IsCompleted && st.ParticipantIds.Contains(participantId));
        }

        public abstract int GetTotalNumClassesForParticipant(string participantId);

        public List<DateTime> GetUpcomingClassTimesUtcForParticipant(string participantId)
        {
            try
            {
                return ClassesInfo.Values
                    .Where(st => !st.IsCompleted && st.ParticipantIds.Contains(participantId))
                    .Select(st => st.StartTime)
                    .ToList();
            }
            catch (InvalidOperationException)
            {
                return new List<DateTime>();
            }
        }

        public List<DateTime> GetClassTimesUtcForParticipant(string participantId)
        {
            try
            {
                return ClassesInfo.Values
                    .Where(st => st.ParticipantIds.Contains(participantId))
                    .Select(st => st.StartTime)
                    .ToList();
            }
            catch (InvalidOperationException)
            {
                return new List<DateTime>();
            }
        }

        public abstract List<ClosestCohealerSessionInfo> GetClosestCohealerSessions(bool fromDashboard = false);
        public abstract List<ClosestCohealerSessionInfo> GetCohealerSessions();

        public abstract List<SessionInfoForReminderViewModel> GetTomorrowSessions(DateTime tomorrowStartMomentUtc, DateTime dayAfterTomorrowStartMomentUtc);

        public abstract Dictionary<string, ClassInfo> ClassesInfo { get; }

        public abstract Dictionary<string, List<string>> RoomsWithParticipants { get; }

        public bool IsParticipantInClass(string classId, string participantId)
        {
            return ClassesInfo.ContainsKey(classId) && ClassesInfo[classId].ParticipantIds.Contains(participantId);
        }

        public bool IsParticipant(string participantId, string roomId)
        {
            return RoomsWithParticipants.ContainsKey(roomId) && RoomsWithParticipants[roomId].Contains(participantId);
        }

        public OperationResult AssignRoomInfoToClass(VideoRoomInfo videoRoomInfo, string classId)
        {
            if (!ClassesInfo.TryGetValue(classId, out var targetClass))
            {
                return OperationResult.Failure($"Unable to find book time with id {classId} in availability times to assign room");
            }

            targetClass.VideoRoomContainer.VideoRoomInfo = videoRoomInfo;

            if (videoRoomInfo.RecordParticipantsOnConnect)
            {
                targetClass.RecordingInfos.Add(
                    new RecordingInfo { RoomId = videoRoomInfo.RoomId, DateCreated = videoRoomInfo.DateCreated, RoomName = videoRoomInfo.RoomName });
            }

            return OperationResult.Success();
        }

        public OperationResult<VideoRoomInfo> GetRoomInfoFromClass(string classId)
        {
            if (!ClassesInfo.TryGetValue(classId, out var targetClass))
            {
                return OperationResult<VideoRoomInfo>.Failure($"Unable to find class with id {classId} to get video room info");
            }

            return OperationResult<VideoRoomInfo>.Success(targetClass.VideoRoomContainer.VideoRoomInfo);
        }

        public OperationResult SetRoomAsClosedById(string roomId)
        {
            var videoRoomInfo = VideoRoomInfos.FirstOrDefault(st => st?.RoomId == roomId);

            if (videoRoomInfo is null)
            {
                return OperationResult.Failure($"Unable to find room with id {roomId} to set as closed inside contribution {Title}");
            }

            videoRoomInfo.IsRunning = false;
            return OperationResult.Success();
        }

        public abstract OperationResult SetClassAsCompleted(string classId);

        public abstract OperationResult SetSelfPacedClassAsCompleted(string classId, string clientId);

        public OperationResult GetAttachment(string documentId)
        {
            var documentToFind = Attachments.FirstOrDefault(a => a.Id == documentId);

            if (documentToFind is null)
            {
                return OperationResult.Failure($"Unable to find document with Id {documentId} in attachments");
            }

            return OperationResult.Success(string.Empty, documentToFind);
        }

        public OperationResult AddAttachment(string sessionId, Document document)
        {
            if (!AttachmentCollection.ContainsKey(sessionId))
            {
                return OperationResult.Failure($"Unable to find session with Id: {sessionId}");
            }

            AttachmentCollection[sessionId].Add(document);

            return OperationResult.Success(string.Empty);
        }

        public OperationResult RemoveAttachment(string sessionId, string documentId)
        {
            if (!AttachmentCollection.ContainsKey(sessionId))
            {
                return OperationResult.Failure($"Unable to find class with Id: {sessionId}");
            }

            var classAttachments = AttachmentCollection[sessionId];
            var documentToRemove = classAttachments.FirstOrDefault(a => a.Id == documentId);

            if (documentToRemove is null)
            {
                return OperationResult.Failure($"Unable to find document with Id {documentId} in attachments in class with Id {sessionId}");
            }

            classAttachments.Remove(documentToRemove);

            return OperationResult.Success(string.Empty);
        }

        public abstract IEnumerable<Document> Attachments { get; }

        public abstract Dictionary<string, IList<Document>> AttachmentCollection { get; }

        public abstract IEnumerable<string> AttachmentsKeys { get; }

        public abstract void RevokeAllClassesBookedByUseId(string userId);

        public abstract bool IsExistedSessionsModificationAllowed(
            ContributionBase existedContribution,
            out string errorMessage,
            out List<EditedBookingWithClientId> editedBookings,
            out List<DeletedBookingWithClientId> deletedBookings,
            out List<Document> deletedAttachments);

        public void UpdateRecordingsInfo(string roomId, string fileName, int? duration)
        {
            var recordingInfo = GetRecordingInfo(roomId);

            if (recordingInfo is null)
            {
                throw new Exception($"Room with Id {roomId} has no recording info");
            }

            recordingInfo.CompositionFileName = fileName;
            recordingInfo.Duration = duration;
            recordingInfo.Status = RecordingStatus.Available;
        }

        public RecordingInfo GetRecordingInfo(string roomId)
        {
            return RecordingInfos.FirstOrDefault(e => e.RoomId == roomId);
        }

        public bool IsRoomRecorded(string roomId)
        {
            return GetRecordingInfo(roomId) != default(RecordingInfo);
        }

        public bool IsOwnerOrPartner(string userId)
        {
            return userId == UserId || Partners.Any(e => e.IsAssigned && e.UserId == userId);
        }

        public bool IsOwnerOrPartnerOrParticipant(string userId, string roomId)
        {
            return IsOwnerOrPartner(userId) || IsParticipant(userId, roomId);
        }

        public OneToOneSessionDataUi OneToOneSessionDataUi { get; set; }

        public List<ContributionPartnerViewModel> ContributionPartners { get; set; }

        public abstract IEnumerable<VideoRoomInfo> VideoRoomInfos { get; }

        public abstract IEnumerable<RecordingInfo> RecordingInfos { get; }
        public string CoachAvatarUrl { get; set; }

        public abstract IEnumerable<string> GetAllIdentitiesInClass(string classId);

        public abstract IEnumerable<CohealerContributionTimeRangeViewModel> GetCohealerContributionTimeRanges(Dictionary<string, string> clients, string contributorTimeZoneId, bool timesInUtc);

        public bool IsRecordingPublic { get; set; }
        public string TimeZoneShortForm { get; set; }
    }
}
