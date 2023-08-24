using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Testimonial;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
	public interface ITestimonialService
	{
		Task<OperationResult> Insert(TestimonialViewModel model);

		Task<OperationResult> Update(string id, TestimonialViewModel model);

		Task<OperationResult> Delete(string id);

		Task<OperationResult> Get(string id);

		Task<OperationResult> GetByContributionId(string contributionId);

		Task<OperationResult> ToggleShowcase(string id);
	}
}
