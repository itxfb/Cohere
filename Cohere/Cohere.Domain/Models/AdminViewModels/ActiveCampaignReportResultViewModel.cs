using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Cohere.Domain.Models.AdminViewModels
{
	public class ActiveCampaignReportResultViewModel
	{
		public List<ActiveCampaignReportItemViewModel> ActiveCampaignReportItems { get; set; } = new List<ActiveCampaignReportItemViewModel>();
	}

	public class ActiveCampaignReportItemViewModel
	{
		public string Email { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string AccountCreatedDate { get; set; }
		public string PaidTier { get; set; }
		public string CanceledDate { get; set; }
		public int NumberOfContributions { get; set; }
		public string FirstContributionCratedDate { get; set; }
		public string ContributionCratedDates { get; set; }
		public string RevenuStatus { get; set; }
		public string LastCohereActivityDate { get; set; }
		public string HasAchieved2MonthsOfRevenue { get; set; }
		public string HasAchieved3ConsecutiveMonthsOfRevenue { get; set; }
	}
}
