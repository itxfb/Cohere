using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using MongoDB.Bson.Serialization.Attributes;

namespace Cohere.Entity.Entities.Contrib
{
    public abstract class ContributionBase : BaseEntity
    {
        public string UserId { get; set; }

        public string Title { get; set; }

        public ContributionStatuses Status { get; set; }

        public List<string> Categories { get; set; }
        public bool IsWorkshop { get; set; }
        public abstract string Type { get; }

        public List<string> PreviewContentUrls { get; set; }

        private string tagline;
        public string ContributionTagline 
        {
            get { return tagline; }
            set
            {
                if(!string.IsNullOrEmpty(value) && value.Length > 200)
                {
                    tagline = value.Substring(0,199);
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

        public string Preparation { get; set; }
        public string ActiveTemplate { get; set; }
        public string WhoIAmLabel { get; set; } = "Bio";

        public string WhatWeDoLabel { get; set; } = "What to Expect";

        public string PurposeLabel { get; set; } = "The Purpose";

        public string PreparationLabel { get; set; } = "How to Prepare";
        public bool ViewPurpose { get; set; }

        public bool ViewWhoIAm { get; set; }
        public bool IsDownloadEnabled { get; set; }
        public bool ViewWhatWeDo { get; set; }

        public bool ViewPreparation { get; set; }
        public string WhoIAmIcon { get; set; }
        public string WhatWeDoIcon { get; set; }
        public string PurposeIcon { get; set; }
        public string PreparationIcon { get; set; }
        public string CustomToS { get; set; }

        public string InstagramUrl { get; set; }

        public string LinkedInUrl { get; set; }

        public string YoutubeUrl { get; set; }
        public bool IsElectronicSignatureActive { get; set; }
        public string FacebookUrl { get; set; }

        public string TiktokUrl { get; set; }

        public string WebsiteUrl { get; set; }

        public string ThankYouPageRedirectUrl { get; set; }

        public List<ContributionLanguageCodes> LanguageCodes { get; set; }

        public bool IsSpeakingLanguageRequired { get; set; }
        public bool IsCustomBrandingColorsActive { get; set; }
        public bool IsLegacyColorsActive { get; set; }
        public bool IsDarkModeEnabled { get; set; }
        public bool IsCoachSelectedDarkModeEnabled { get; set; }
        public Dictionary<string, string> BrandingColors { get; set; }
        public Dictionary<string, string> CoachSelectedBrandingColors { get; set; }
        public string MinAge { get; set; }

        public bool IsMembersHiddenInCommunity { get; set; }
        public bool IsMembersHiddenInGroupChat { get; set; }
        public bool IsGroupChatHidden { get; set; }
        public bool IsLiveSessionMembersHidden { get; set; }
        public ContributionGenders Gender { get; set; }

        public PaymentInfo PaymentInfo { get; set; }

        public PaymentTypes PaymentType { get; set; }

        public TaxTypes TaxType { get; set; }

        public bool IsInvoiced { get; set; }

        public bool ArePublicPostsAllowed { get; set; }
        public string CustomLogo { get; set; }
        public bool IsCommunityFeatureHiddden { get; set; } = false;
        public bool EnableCommunityPosts { get; set; } = true;
        public bool AreClientPublicPostsAllowed { get; set; }

        public bool InvitationOnly { get; set; }

        public bool HasAgreedContributionTerms { get; set; }

        public float Rating { get; set; }

        public int LikesNumber { get; set; }

        public string DefaultCurrency { get; set; } = "usd";

        public string DefaultSymbol { get; set; } = "$";
        public string ExternalCalendarEmail { get; set; }

        public List<Review> Reviews { get; set; }
        public string CustomTitleForMeetYourCoach { get; set; }
        public LiveVideoProvider LiveVideoServiceProvider { get; set; } = new LiveVideoProvider { ProviderName = "Cohere" };

        public string CustomInvitationBody { get; set; }
        public TimeRange TimeRangeUtc { get; set; }

        public List<AdminReviewNote> AdminReviewNotes { get; set; } = new List<AdminReviewNote>();

        public List<TimeRange> CohealerBusyTimeRanges { get; set; }

        public GroupChat Chat { get; set; }

        public List<ContributionPartner> Partners { get; set; } = new List<ContributionPartner>();

        public List<long> ZoomMeetigsIds { get; set; } = new List<long>();

        public abstract bool IsCompletedTimesChanged(ContributionBase contribution, out string errorMessage);

        [BsonElement]
        public abstract List<string> RecordedRooms { get; }

        public abstract void CleanSessions();

        public bool ParticipantHasAccessToContribution(ContributionBase contribution, string participantId)
        {
            if (contribution is SessionBasedContribution sessionBasedContribution)
            {
                var sessionTimes = sessionBasedContribution.Sessions.SelectMany(x => x.SessionTimes);

                foreach (var sessionTime in sessionTimes)
                {
                    if (sessionTime.ParticipantsIds.Any(x => x == participantId))
                    {
                        return true;
                    };
                }
            }

            return false;
        }

        public bool IsRecordingPublic { get; set; }
        public bool DeletingAllowed { get; set; } = false;
    }
}
