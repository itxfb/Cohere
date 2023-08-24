namespace Cohere.Domain.Utils
{
    public static class Constants
    {
        public const int MinCohealerAge = 18;
        public const string TimeZoneIdDefault = "America/Los_Angeles";
        public const string LanguageCodeDefault = "en";
        public const int MaxSocialMediaLinksToHave = 15;
        public const int MaxSecurityQuestionsNumber = 5;
        public const int MinSecurityQuestionsNumber = 3;
        public const int CohealerDashboardContributionsCount = 3;
        public const int DateTimeSearchAccuracyMins = 1;
        public static readonly string[] BadWordsArray = { "fuck", "bitch" };
        public static readonly string DefaultStripeAccount = Constants.Stripe.AccountType.Standard;

        public static class LiveVideoProviders
        {
            public const string DefaultLocationUrl = "https://app.cohere.live";
            public const string Cohere = "Cohere";
            public const string Custom = "Custom";
            public const string Zoom = "Zoom";
        }

        public static class CohereLegacyColors
        {
            public const string PrimaryColorCode = "#CDBA8F";
            public const string AccentColorCode = "#116582";
            public const string TertiaryColorCode = "#F6E8BO";
            public const string TextColorCode = "Auto";
        }
        public static class PaidTierTitles
        {
            public const string Launch = "Launch";
            public const string Impact = "Impact";
            public const string Scale = "Scale"; 
        }

        public static class TemplatesPaths
        {
            public const string DirectoryBaseName = "Templates";

            public static class Affiliate
            {
                public static string ShareReferal = $"{DirectoryBaseName}/{DirectoryName}/ShareReferal.html";

                private const string DirectoryName = nameof(Affiliate);
            }

            public static class Email
            {
                public static string Confirmation = $"{DirectoryBaseName}/{DirectoryName}/Confirmation.html";
                public static string PasswordRestoration = $"{DirectoryBaseName}/{DirectoryName}/PasswordRestoration.html";

                private const string DirectoryName = nameof(Email);
            }

            public static class Contribution
            {
                public static string StatusChanged = $"{DirectoryBaseName}/{DirectoryName}/StatusChanged.html";
                public static string ReadyForReview = $"{DirectoryBaseName}/{DirectoryName}/ReadyForReview.html";
                public static string ShareContribution = $"{DirectoryBaseName}/{DirectoryName}/ShareContribution.html";
                public static string PaymentNotification = $"{DirectoryBaseName}/{DirectoryName}/PaymentNotification.html";
                public static string NewSale = $"{DirectoryBaseName}/{DirectoryName}/NewSale.html";
                public static string NewFreeSale = $"{DirectoryBaseName}/{DirectoryName}/NewFreeSale.html";
                public static string CohealerSessionReminder = $"{DirectoryBaseName}/{DirectoryName}/CohealerSessionReminder.html";
                public static string ClientSessionReminder = $"{DirectoryBaseName}/{DirectoryName}/ClientSessionReminder.html";
                public static string ClientSessionOneHourReminder = $"{DirectoryBaseName}/{DirectoryName}/ClientSessionOneHourReminder.html";
                public static string ClientPurchaseError = $"{DirectoryBaseName}/{DirectoryName}/ClientPurchaseError.html";
                public static string TransferMoneyNotification = $"{DirectoryBaseName}/{DirectoryName}/TransferMoneyNotification.html";
                public static string ClientBookedTimeDeleted = $"{DirectoryBaseName}/{DirectoryName}/ClientBookedTimeDeleted.html";
                public static string ClientBookedTimeEdited = $"{DirectoryBaseName}/{DirectoryName}/ClientBookedTimeEdited.html";
                public static string CreateContributionGuide = $"{DirectoryBaseName}/{DirectoryName}/CreateContributionGuide.html";
                public static string SendEmailPartnerCoachInvite = $"{DirectoryBaseName}/{DirectoryName}/PartnerCoachInvite.html";
                public static string CreateOneToOneContributionGuide = $"{DirectoryBaseName}/{DirectoryName}/CreateOneToOneContributionGuide.html";
                public static string ShareContributionGuide = $"{DirectoryBaseName}/{DirectoryName}/ShareContributionGuide.html";
                public static string ContributionSessionsWasUpdatedNotification = $"{DirectoryBaseName}/{DirectoryName}/ContributionSessionsWasUpdatedNotification.html";
                public static string ContributionSessionsWasUpdatedNotificationForNylas = $"{DirectoryBaseName}/{DirectoryName}/ContributionSessionsWasUpdatedNotificationForNylas.html";
                public static string OneToOneWasRescheduled = $"{DirectoryBaseName}/{DirectoryName}/OneToOneWasRescheduled.html";
                public static string NewRecordingsAvailable = $"{DirectoryBaseName}/{DirectoryName}/NewRecordingsAvailable.html";
                public static string UploadedFileToContribution = $"{DirectoryBaseName}/{DirectoryName}/UploadedFileToContribution.html";
                public static string UserWasTaggedNotification = $"{DirectoryBaseName}/{DirectoryName}/UserWasTaggedNotification.html";

                private const string DirectoryName = nameof(Contribution);
            }

            public static class Communication
            {
                public static string CustomCohealerMessage = $"{DirectoryBaseName}/{DirectoryName}/CustomCohealerMessage.html";
                public static string UnreadConversationGeneral = $"{DirectoryBaseName}/{DirectoryName}/UnreadConversationGeneral.html";
                public static string GroupConversation = $"{DirectoryBaseName}/{DirectoryName}/GroupConversation.html";
                public static string OneToOneConversation = $"{DirectoryBaseName}/{DirectoryName}/1To1Conversation.html";

                private const string DirectoryName = nameof(Communication);
            }

            public static class Account
            {
                public static string ClientEmailConfirmation = $"{DirectoryBaseName}/{DirectoryName}/ClientEmailConfirmation.html";
                public static string CohealerEmailConfirmation = $"{DirectoryBaseName}/{DirectoryName}/CohealerEmailConfirmation.html";
                public static string PasswordResetEmail = $"{DirectoryBaseName}/{DirectoryName}/PasswordResetEmail.html";
                public static string NewCohealerInstructions = $"{DirectoryBaseName}/{DirectoryName}/NewCohealerInstructions.html";
                public static string NewCohealerAdminNotification = $"{DirectoryBaseName}/{DirectoryName}/NewCohealerAdminNotification.html";
                public static string PaidtierAccountCancellationEmail = $"{DirectoryBaseName}/{DirectoryName}/PaidtierAccountCancellationEmail.html";
                public static string CancelledPlanExpirationAlertNotification = $"{DirectoryBaseName}/{DirectoryName}/CancelledPlanExpirationAlertNotification.html";
                public static string NewPaidtierAccountNotification = $"{DirectoryBaseName}/{DirectoryName}/NewPaidtierAccountNotification.html";

                private const string DirectoryName = nameof(Account);
            }

            public static class Payment
            {
                public static string FailedPaymentNotification = $"{DirectoryBaseName}/{DirectoryName}/FailedPaymentNotification.html";
                public static string InvoicePaidEmail = $"{DirectoryBaseName}/{DirectoryName}/InvoicePaidEmail.html";
                public static string InvoiceDueEmail = $"{DirectoryBaseName}/{DirectoryName}/InvoiceDueEmail.html";
                public static string InvoiceDueEmailForCoach = $"{DirectoryBaseName}/{DirectoryName}/InvoiceDueEmailForCoach.html";
                private const string DirectoryName = nameof(Payment);
            }
        }

        public static class Contribution
        {
            public static class Payment
            {
                public const string MetadataIdKey = "ContributionId";
                public const string AvailabilityTimeIdBookedTimeIdPairsKey = "AvailabilityTimeIdBookedTimeIdPairs";
                public const string TransferMoneyDataKey = "TransferMoneyData"; 
                public const string BookOneToOneTimeViewModel = "BookOneToOneTimeViewModel";

                public static class Statuses
                {
                    public const string Unpurchased = "unpurchased";
                    public const string ProceedSubscription = "proceed_subscription";
                }

                public static class ConnectAccount
                {
                    public static class TransfersCapability
                    {
                        public const string Active = "active";
                        public const string Inactive = "inactive";
                    }
                }

                public static class StripeWebhookErrors
                {
                    public const string ContributionNotFound = "Contribution was not found";
                    public const string UserNotFound = "User was not found";
                    public const string TotalAMountNotFound = "Total amount not found";
                }
            }

            public static class Dashboard
            {
                public const int MinutesMaxToShowCohealerClosestSessionBanner = 60;
                public const int MinutesMaxToShowClientClosestSessionBanner = 60;

                public static class SalesRepresentation
                {
                    public const string LiveCourse = "Live Group Courses";
                    public const string OneToOne = "Individual Sessions";
                    public const string Membership = "Memberships";
                    public const string Community = "Community";
                }

                //public static class UpcomingSessionsRepresentation
                //{
                //    public const string LiveCourse = "Live Course";
                //    public const string OneToOne = "1:1";
                //}
            }
        }

        public static class Chat
        {
            public const int TimeToWaitTaskCompletedMilliseconds = 30000;
            public const int NumberOfRetry = 10;
            public const int SendFirstUnreadNotificationInMinutes = 5;
            public const int SendSecondUnreadNotificationInDays = 7;
        }

        public static class Stripe
        {
            public static class MetadataKeys
            {
                public const string CohealerId = "CohealerId";
                public const string ContributionId = "ContributionId";
                public const string BookedTimeId = "BookedTimeId";
                public const string PurchaseId = "PurchaseId";
                public const string IsAffiliateRevenue = "IsAffiliateRevenue";
                public const string PaidTierId = "PaidTierId";
                public const string PaymentOption = "PaymentOption";
                public const string SplitNumbers = "SplitNumbers";
                public const string CouponId = "CouponId";
                public const string AvailabilityTimeId = "AvailabilityTimeId"; 
            }

            public static class AccountType
            {
                public const string Standard = "standard";
                public const string Custom = "custom";
                public const string Express = "express";
            }
        }
    }
}
