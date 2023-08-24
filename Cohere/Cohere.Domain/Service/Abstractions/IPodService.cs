using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Pods;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
	public interface IPodService
	{
		Task<OperationResult> Insert(PodViewModel model);

		Task<OperationResult> GetByUserId(string userId);

		Task<OperationResult> Update(string id, PodViewModel model);

		Task<OperationResult> Delete(string id);

		Task<OperationResult> Get(string id);

		Task<OperationResult> GetByContributionId(string contributionId);
	}
}
