using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Entities
{
	public class TimeZone : BaseEntity
	{
		public string Name { get; set; }

		public string CountryName { get; set; }

		public string CountryId { get; set; }

		public bool IsFavourite { set; get; }
		public string ShortName { set; get; }
	}
}
