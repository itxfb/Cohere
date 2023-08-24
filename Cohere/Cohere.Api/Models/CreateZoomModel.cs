using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.Models
{
	public class CreateZoomModel
	{
		public string AuthToken { get; set; }

		public string RedirectUri { get; set; }
	}
}
