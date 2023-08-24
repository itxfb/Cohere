using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Pods;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
	public class PodService : IPodService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IMapper _mapper;

		public PodService(IUnitOfWork unitOfWork, IMapper mapper)
		{
			_unitOfWork = unitOfWork;
			_mapper = mapper;
		}

		public async Task<OperationResult> GetByUserId(string userId)
		{
			var pods = await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => x.CoachId == userId);

			return OperationResult.Success(null, _mapper.Map<List<PodViewModel>>(pods));
		}

		public async Task<OperationResult> GetByContributionId(string contributionId)
		{
			var pods = await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => x.ContributionId == contributionId);

			return OperationResult.Success(null, _mapper.Map<List<PodViewModel>>(pods));
		}

		public async Task<OperationResult> Insert(PodViewModel model)
		{
			var pod = _mapper.Map<Pod>(model);

			await _unitOfWork.GetRepositoryAsync<Pod>().Insert(pod);

			return OperationResult.Success(null, _mapper.Map<PodViewModel>(pod));
		}

		public async Task<OperationResult> Get(string id)
		{
			var pod = await _unitOfWork.GetRepositoryAsync<Pod>().GetOne(x => x.Id == id);

			return OperationResult.Success(null, _mapper.Map<PodViewModel>(pod));
		}

		public async Task<OperationResult> Delete(string id)
		{
			await _unitOfWork.GetRepositoryAsync<Pod>().Delete(id);

			return OperationResult.Success();
		}

		public async Task<OperationResult> Update(string id, PodViewModel model)
		{
			var pod = _mapper.Map<Pod>(model);
			await _unitOfWork.GetRepositoryAsync<Pod>().Update(id, pod);
			return OperationResult.Success(null, _mapper.Map<PodViewModel>(pod));
		}
	}
}
