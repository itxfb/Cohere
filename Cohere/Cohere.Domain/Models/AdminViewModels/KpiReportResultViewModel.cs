using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Cohere.Domain.Models.AdminViewModels
{
	public class KpiReportResultViewModel
	{
		public int NumberOfNewLaunchPlanUsersWithAccountAndNoContributions { get; set; }
		public int NumberOfTotalLaunchPlanUsersWithAccountAndNoContributions { get; set; }
		public int NumberOfNewLaunchPlanUsersWithContributionAndNoSales { get; set; }
		public int NumberOfTotalLaunchPlanUsersWithContributionAndNoSales { get; set; }
		public int NumberOfNewLaunchPlanUsersMadeSales { get; set; }
		public int NumberOfTotalLaunchPlanUsersMadeSales { get; set; }
		public int NumberOfNewPaidTierPlansUsersWithAccountAndNoContributions { get; set; }
		public int NumberOfTotalPaidTierPlansUsersWithAccountAndNoContributions { get; set; }
		public int NumberOfNewPaidTierPlansUsersWithContributionAndNoSales { get; set; }
		public int NumberOfTotalPaidTierPlansUsersWithContributionAndNoSales { get; set; }
		public int NumberOfNewPaidTierPlansUsersMadeSales { get; set; }
		public int NumberOfTotalPaidTierPlansUsersMadeSales { get; set; }
		public int NumberOfNewPaidTiers { get; set; }
		public int NumberOfTotalPaidTiers { get; set; }
		public int NumberOfLaunchPlanMembersMadeSalesDuringReportTime { get; set; }
		public int NumberOfPaidTierPlansUsersMadeSalesDuringReportTime { get; set; }
		public int NumberOfNewAccounts { get; set; }
		public int TotalNumberOfReferrals { get; set; }
		public int TotalNumberOfNewReferrals { get; set; }
		public int TotalNumberOfReferralsWithSales { get; set; }
		public int TotalNumberOfNewReferralsWithSales { get; set; }
		public int NumberOfNewActivePaidTierCoaches { get; set; }
		public int NumberOfTotalActivePaidTierCoaches { get; set; }
		public int NumberOfNewActiveFreeTierCoaches { get; set; }
		public int NumberOfTotalActiveFreeTierCoaches { get; set; }
		public int NumberOfNewCanceledPaidTiers { get; set; }
		public int NumberOfTotalCanceledPaidTiers { get; set; }

	}
}
