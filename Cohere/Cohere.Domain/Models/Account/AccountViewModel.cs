using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Account;
using Cohere.Entity.Enums.User;

namespace Cohere.Domain.Models.Account
{
    public class AccountViewModel : BaseDomain
    {
        private string _emailLower;

        public string Email
        {
            get => _emailLower?.ToLower();
            set => _emailLower = value;
        }

        public string Password { get; set; }

        [JsonIgnore]
        public List<Roles> Roles { get; set; }
        //TODO: This property will be removed once testing with front end is done
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

        public AccountPreferencesViewModel AccountPreferences { get; set; }

        public bool PaidTierOptionBannerHidden { get; set; }

        public CoachLoginInfo CoachLoginInfo { get; set; }
        
        [JsonIgnore]
        public string ZoomRefreshToken { get; set; }

        public bool IsZoomEnabled 
        { 
            get 
            {
                return !string.IsNullOrWhiteSpace(this.ZoomRefreshToken); 
            } 
        }

        public DateTime? TrialStartedTime { get; set; }

        public SignupTypes SignupType { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TrialStatus TrialStatus
        {
            get
            {
                return TrialStartedTime == null ? TrialStatus.NotStarted :
                    (DateTime.UtcNow - (DateTime)TrialStartedTime).TotalSeconds > 1209600 // 14 days
                    ? TrialStatus.Completed : TrialStatus.InProgress;
            }
        }
    }
}