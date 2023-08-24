using System;
using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Affiliate;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Account;
using Cohere.Entity.Enums.User;
using Cohere.Entity.Utils;

namespace Cohere.Entity.Entities
{
    public class Account : BaseEntity
    {
        public string Email { get; set; }

        // TODO once working double check password set -> encryption flow
        public string EncryptedPassword { get; set; }

        public string DecryptedPassword
        {
            get => EntityHelper.Decrypt(EncryptedPassword, EncryptionSalt);
            set
            {
                var encryptionResult = EntityHelper.Encrypt(value);
                EncryptedPassword = encryptionResult.EncryptedPassword;
                EncryptionSalt = encryptionResult.EncryptionSaltString;
            }
        }

        public string EncryptionSalt { get; set; }

        public List<Roles> Roles { get; set; }

        public string VerificationToken { get; set; }

        public DateTime VerificationTokenExpiration { get; set; }

        public string PasswordRestorationToken { get; set; }

        public DateTime PasswordRestorationTokenExpiration { get; set; }

        public string OAuthToken { get; set; }

        public OnboardingStatuses OnboardingStatus { get; set; }

        public Dictionary<string, string> SecurityAnswers { get; set; }

        public int NumLogonAttempts { get; set; }

        public bool IsEmailConfirmed { get; set; }

        public bool IsBankAccountConnected { get; set; }

        public bool IsStandardBankAccountConnected { get; set; }

        public bool IsPhoneConfirmed { get; set; }

        public bool IsAccountLocked { get; set; }

        public bool IsPushNotificationsEnabled { get; set; }

        public bool IsEmailNotificationsEnabled { get; set; }

        public bool IsVideoTestFirstTime { get; set; }

        public string InvitedBy { get; set; }

        public string ZoomRefreshToken { get; set; }

        public string ZoomUserId { get; set; }

        public DateTime? TrialStartedTime { get; set; }

        public AffiliateRevenueLimitsModel AffiliateRevenueLimits { get; set; }

        public CoachLoginInfo CoachLoginInfo { get; set; }

        // TODO: add migration to set default affiliate program config for existed coach account
        public AffiliateProgramConfigurationModel AffiliateProgramConfiguration { get; set; } = new AffiliateProgramConfigurationModel
        {
            AffiliateFee = 50L,
            FromReferralPaidTierFee = 50L,
            MaxRevenuePerReferral = 500m, // TODO: 500$
            MaxPeriodPerReferal = 12 // TODO: 12 Months
        };

        public bool UnenrolledAffiliate { get; set; }

        public AccountPreferences AccountPreferences { get; set; }

        public bool PaidTierOptionBannerHidden { get; set; }

        public SignupTypes SignupType { get; set; }
        public Dictionary<string, bool> UserProgressbarData { get; set; }
    }
}
