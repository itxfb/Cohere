using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignDeal
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("contact")]
        public string Contact { get; set; }

        [JsonPropertyName("account")]
        public string Account { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("group")]
        public string Group { get; set; }

        [JsonPropertyName("owner")]
        public string Owner { get; set; }
        
        [JsonPropertyName("percent")]
        public string Percent { get; set; }

        [JsonPropertyName("cdate")]
        public string cDate { get; set; }

        [JsonPropertyName("stage")] 
        public string Stage { get; set; }

        [JsonPropertyName("status")]
        public int? Status { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("fields")]
        public List<ActiveCampaignCustomFields> Fields { get; set; } = new List<ActiveCampaignCustomFields>();
    }
    public class ActiveCampaignDealRequest
    {
        [JsonPropertyName("deal")]
        public ActiveCampaignDeal Deal { get; set; }
    }

    public class ActiveCampaignDealsRequest
    {
        [JsonPropertyName("deals")]
        public IEnumerable<ActiveCampaignDeal> Deals { get; set; }
    }

    public class ActiveCampaignDealResponse
    {
        [JsonPropertyName("deal")]
        public ActiveCampaignDeal Deal { get; set; }
    }

    public class DealPipeline
	{
        public string CoachProspects = "0. Coach Prospects";
        public string NewLunchPlan = "[NEW] Launch Plan";
        public string NewPaidTierPlan = "[NEW] Paid Tier";

    }

	public class DealStage
	{
		public string Stage0 = "0 - Purchased List or OTHER";
		public string Stage0a = "0a - Cold";
		public string Stage0b = "0b - Warm";
		public string Stage0c = "0c -Qualified";
		public string Stage0d = "0d - Closing";
		public string Stage0x = "0x - Product doesn't support expectations";
		public string Stage0z = "0z - Currently not interested";

		public string Stage1a = "1a - account created 30 days ago or less";
		public string Stage1b = "1b - account created 31-90 days ago";
		public string Stage1c = "1c - account created over 90 days ago";
		public string Stage2a = "2a - contribution created in last 30 days";
		public string Stage2b = "2b - contribution created 30-90 days ago";
		public string Stage2c = "2c - contribution created over 90 days ago";
		public string Stage3a = "3a - first revenues achieved within 1 month";
		public string Stage3b = "3b - revenue achieved for at least 2 months (does not have to be consecutive months)";
		public string Stage3c = "3c - revenue achieved 3+ consecutive months (edited)";
		public string StageWON = "WON - Moved to Paid Tier";

		public string StageToContact = "To Contact";
		public string StageHighImpactOpportunties = "High Impact Opportunties";
		public string StageQualified = "Qualified";
		public string StageInContracting = "In Contracting";
		public string StageClosing = "Closing";
		public string StageDeclinedOrSleeping = "Declined or Sleeping";

        public string StageKickOffCallAndNextSteps = "Kick off call and next steps";
        public string StageInLaunchPlanning = "In Launch Planning";
        public string StageLaunchingLessThan30Days = "Launching < 30 days";
    }

    public class ActiveCampaignDealMessageKey
	{
        public string PaidTier = "_pt";
        public string AccountCancelDate = "_acd";
        public string ContributionStatus = "_cs";
        public string Revenue = "_revenue";
        public string HasAchieved2MonthsOfRevenue = "_ha2";
        public string HasAchieved3ConsecutiveMonthsOfRevenue = "_ha3";
        public string LastCohereActivity = "_lca";
        public string InvitedBy = "_ib";
        public string FirstContributionCreationDate = "_fccd";
        public string PaidTierCreditCardStatus = "_ptccs";
        public string NumberOfReferrals = "_nor";
        public string AffiliateRevenueEarned = "_are";
    }

    public class DealCustomFieldLabel
	{
        public string ContributionStatus = "Contribution Status";
        public string PaidTier = "Paid Tier";
        public string AccountCancelDate = "Account Cancel Date";
        public string Revenue = "Revenue Status";
        public string HasAchieved2MonthsOfRevenue = "Has achieved 2 Months of Revenue";
        public string HasAchieved3ConsecutiveMonthsOfRevenue = "Has Achieved 3 Consecutive Months of Revenue";
        public string LastCohereActivity = "Last Cohere Activity";
        public string InvitedBy = "Referred By";
        public string FirstContributionCreationDate = "1st Contribution Creation Date";
        public string PaidTierCreditCardStatus = "Paid Tier Credit Card Status";
        public string NumberOfReferrals = "Number of Referrals";
        public string AffiliateRevenueEarned = "Affiliate Revenue Earned";
    }

    public class ActiveCampaignDealCustomFieldOptions
	{
        public string CohereAccountId { get; set; }
        public string StageName { get; set; }
        public string PipelineName { get; set; }
        public string PaidTier { get; set; }
        public string AccountCancelDate { get; set; }
        public string ContributionStatus { get; set; }
        public string Revenue { get; set; }
        public string HasAchieved2MonthsOfRevenue { get; set; }
        public string HasAchieved3ConsecutiveMonthsOfRevenue { get; set; }
        public string LastCohereActivity { get; set; }
        public string InvitedBy { get; set; }
        public string FirstContributionCreationDate { get; set; }
        public string PaidTierCreditCardStatus { get; set; }
        public string NumberOfReferrals { get; set; }
        public string AffiliateRevenueEarned { get; set; }
    }

    public enum DealStatus
	{
        Open = 0,
        Won = 1,
        Lost = 2,
	}

    public enum ContributionStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,
        [Display(Name = "Live")]
        Live = 2,
    }

    public enum Revenue
    {
        [Display(Name = "Pre-Revenue")]
        PreRevenue = 1,
        [Display(Name = "Revenue")]
        Revenue = 2,
        [Display(Name = "Monthly Revenue")]
        MonthlyRevenue = 2
    }

    public enum HasAchieved2MonthsOfRevenue
	{
        [Display(Name = "Yes")]
        Yes = 1,
        [Display(Name = "No")]
        No = 2
    }
    public enum HasAchieved3ConsecutiveMonthsOfRevenue
    {
        [Display(Name = "Yes")]
        Yes = 1,
        [Display(Name = "No")]
        No = 2
    }

    public enum CreditCardStatus
    {
        [Display(Name = "Normal")]
        Normal = 1,
        [Display(Name = "Failed ")]
        Failed = 2,
    }

}
