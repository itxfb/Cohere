using System;
using System.Collections.Generic;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.EntitiesAuxiliary.User;

namespace Cohere.Domain.Models.User
{
    public class UserViewModel : BaseDomain
    {
        public string AccountId { get; set; }

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string MiddleName { get; set; }

        public string LastName { get; set; }

        public string NameSuffix { get; set; }

        public string AvatarUrl { get; set; }

        public bool HasAgreedToTerms { get; set; }
        public string CustomLogo { get; set; }
        public Dictionary<string, string> BrandingColors { get; set; }
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

        public bool IsPartnerCoach { set; get; }

        public Phone Phone1 { get; set; }

        public Phone Phone2 { get; set; }

        public string PlaidId { get; set; }

        public string CustomerStripeAccountId { get; set; }

        // Cohealer properties

        public string ConnectedStripeAccountId { get; set; }

        //for seperate plateform controlled standard  account
        public bool IsStandardAccount { get; set; }
        public string StripeStandardAccountId { get; set; }
        public bool StandardAccountTransfersEnabled { get; set; }

        public bool IsStandardTaxEnabled { set; get; }
        public string DefaultPaymentMethod { get; set; }

        public string BusinessName { get; set; }

        public string BusinessType { get; set; }

        public string CustomBusinessType { get; set; }

        public Dictionary<string, string> SocialMediaLinks { get; set; }

        public string Certification { get; set; }

        public string Occupation { get; set; }

        public string CustomerLabelPreference { get; set; }

        public bool PayoutsEnabled { get; set; }

        public bool TransfersEnabled { get; set; }

        public bool TransfersNotLimited { get; set; }

        public bool IsSocialSecurityCheckPassed { get; set; }

        public bool IsFirstAcceptedCourseExists { get; set; }

        public string ServiceAgreementType { get; set; }
        public bool IsBetaUser { get; set; }
        public string OldConnectedStripeAccountId { get; set; }
        public string ProfileLinkName { get; set; }
        public bool EnableCustomEmailNotification { get; set; }
        public ProfilePageViewModel ProfilePageViewModel { get; set; }
        public Dictionary<string, bool> UserProgressbarData { get; set; }
        public int ProgressBarPercentage { get; set; }



    }
}
