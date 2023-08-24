using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Entities
{
	public class Testimonial : BaseEntity
	{
		public string ContributionId { get; set; }

		public string Name { get; set; }

		public string Role { get; set; }

		public string AvatarUrl { get; set; }

		public string Description { get; set; }

		public bool AddedToShowcase { get; set; }
	}
}
