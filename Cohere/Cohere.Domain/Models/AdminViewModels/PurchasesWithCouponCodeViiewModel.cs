using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.AdminViewModels
{
	public class PurchasesWithCouponCodeViiewModel
	{
		public string CouponId { get; set; }
		public string PurchaseId { get; set; }
		public string ClientEmail { get; set; }
		public DateTime DateOfPurchase { get; set; }
	}
}
