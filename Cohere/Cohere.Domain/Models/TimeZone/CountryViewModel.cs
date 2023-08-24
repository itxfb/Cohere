using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.TimeZone
{
	public class CountryViewModel : BaseDomain
	{
		public string Name { get; set; }

		public string Alpha2Code { get; set; }

		public bool StripeSupportedCountry { get; set; }
	}
}
