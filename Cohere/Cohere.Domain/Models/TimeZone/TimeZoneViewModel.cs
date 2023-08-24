using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.TimeZone
{
	public class TimeZoneViewModel : BaseDomain
	{
		public string Name { get; set; }

		public string CountryName { get; set; }

		public string CountryId { get; set; }

        public bool IsFavourite { set; get; }
    }
}
