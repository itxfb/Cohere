using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.Testimonial
{
	public class TestimonialViewModel : BaseDomain
	{
		public string ContributionId { get; set; }

		public string Name { get; set; }

		public string Role { get; set; }

		public string AvatarUrl { get; set; }

		public string Description { get; set; }

		public bool AddedToShowcase { get; set; }
	}
}
