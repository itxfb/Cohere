using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Entities
{
	public class Country : BaseEntity
	{
		public string Name { get; set; }

		public string Alpha2Code { get; set; }

		public bool StripeSupportedCountry { get; set; }
	}
}
