using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;

namespace Cohere.Domain.Models.AdminViewModels
{
	public class LinkingStripePurchasesViewModel
	{
		[Required]
		public string Email { get; set; }

		[Required]
		public string SubscriptionId { get; set; }

		[Required]
		public string TransactionId { get; set; }

		[Required]
		public string PaymentOption { get; set; }

		[Required]
		public decimal TransferAmount { get; set; }

		[Required]
		public decimal PurchaseAmount { get; set; }

		[Required]
		public decimal GrossPurchaseAmount { get; set; }

		[Required]
		public DateTime PeriodEnds { get; set; }
	}
}
