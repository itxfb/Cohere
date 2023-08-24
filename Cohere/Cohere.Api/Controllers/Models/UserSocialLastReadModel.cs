using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers.Models
{
	public class UserSocialLastReadModel
	{
		public string UserId { get; set; }
		
		public string ContributionId { get; set; }
	}
}
