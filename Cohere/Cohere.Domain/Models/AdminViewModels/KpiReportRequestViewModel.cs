using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Cohere.Domain.Models.AdminViewModels
{
	public class KpiReportRequestViewModel
	{
		[Required]
		public DateTime From { get; set; }
		
		[Required]
		public DateTime To { get; set; }
	}
}
