using System;
using System.Collections.Generic;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.User;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.User;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Entity.Entities
{
    public class User : BaseEntity
    {
        public User()
		{
            PaidTierPurchases = new List<PaidTierPurchase>();
            Contributions = new List<ContributionBase>();
            Purchases = new List<Purchase>();
            Pods = new List<Pod>();
        }

        public string AccountId { get; set; }

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string NameSuffix { get; set; }

        public string AvatarUrl { get; set; }

        public bool HasAgreedToTerms { get; set; }

        public bool IsCohealer { get; set; }

        public ClientPreferences ClientPreferences { get; set; }

        public DateTime BirthDate { get; set; }

        public string SocialSecurityNumber { get; set; }

        public string StreetAddress { get; set; }

        public string Apt { get; set; }

        public string City { get; set; }

        public string StateCode { get; set; }

        public string Zip { get; set; }

        public string CountryCode { get; set; }

        public string Bio { get; set; }

        public string TimeZoneId { get; set; }

        public string CountryId { get; set; }

        public string LanguageCode { get; set; }

        public Location Location { get; set; }

        public List<string> DeviceTokenIds { get; set; } = new List<string>();

        public List<NotificationCategory> NotificationCategories { get; set; } = new List<NotificationCategory>();

        public bool IsPermissionsUpdated { get; set; }

        public bool IsPartnerCoach { get; set; }

        public Phone Phone1 { get; set; }

        public Phone Phone2 { get; set; }

        // TODO once have payment services connected check if [Required] should be add to all props related to payments
        public string PlaidId { get; set; }

        public string CustomerStripeAccountId { get; set; }

        // Cohealer properties
        public string ConnectedStripeAccountId { get; set; }

        //for seperate plateform controlled connected account
        public bool IsStandardAccount { get; set; } = false;
        public bool IsStandardTaxEnabled { set; get; }
        public string StripeStandardAccountId { get; set; }
        public bool StandardAccountTransfersEnabled { get; set; }
        public bool StandardAccountTransfersNotLimited { get; set; }

        public PaymentTypes DefaultPaymentMethod { get; set; }

        public string BusinessName { get; set; }

        public BusinessTypes BusinessType { get; set; }

        public string CustomBusinessType { get; set; }

        public string Certification { get; set; }

        public string Occupation { get; set; }

        public CustomerLabelPreferences CustomerLabelPreference { get; set; }

        public bool PayoutsEnabled { get; set; }
        public string CustomLogo { get; set; }
        public Dictionary<string, string> BrandingColors { get; set; }
        public bool TransfersEnabled { get; set; }

        public bool TransfersNotLimited { get; set; }

        public bool FirstContributionGuideSent { get; set; }

        public bool IsSocialSecurityCheckPassed { get; set; }

        public bool IsFirstAcceptedCourseExists { get; set; }

        public Dictionary<string, DateTime> LastReadSocialInfos { get; set; } = new Dictionary<string, DateTime>();
        public Dictionary<string, DateTime> PostLastSeen { get; set; } = new Dictionary<string, DateTime>();
        public virtual IEnumerable<PaidTierPurchase> PaidTierPurchases { get; set; }
        public virtual IEnumerable<ContributionBase> Contributions { get; set; }
        public virtual IEnumerable<Pod> Pods { get; set; }
        public virtual IEnumerable<Purchase> Purchases { get; set; }
        public virtual IEnumerable<UserActivity> UserActivities { get; set; }
        public virtual Account Account { get; set; }
        public string ServiceAgreementType { get; set; }
        public bool IsBetaUser { get; set; } = false;
        public string OldConnectedStripeAccountId { get; set; }
        public string ProfileLinkName { get; set; }
        public bool EnableCustomEmailNotification { get; set; }
    }
}
