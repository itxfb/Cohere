using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.Testimonial;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
	public class TestimonialService : ITestimonialService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IMapper _mapper;
		private readonly IContentService _contentService;

		public TestimonialService(IUnitOfWork unitOfWork, IMapper mapper, IContentService contentService)
		{
			_unitOfWork = unitOfWork;
			_mapper = mapper;
			_contentService = contentService;
		}

		public async Task<OperationResult> ToggleShowcase(string id)
		{
			var testimonial = await _unitOfWork.GetRepositoryAsync<Testimonial>().GetOne(x => x.Id == id);
			testimonial.AddedToShowcase = !testimonial.AddedToShowcase;
			await _unitOfWork.GetRepositoryAsync<Testimonial>().Update(id, testimonial);
			var testimonialVmResult = _mapper.Map<TestimonialViewModel>(testimonial);

			return OperationResult.Success(null, testimonialVmResult);
		}

		public async Task<OperationResult> Delete(string id)
		{
			var testimonial = await _unitOfWork.GetRepositoryAsync<Testimonial>().GetOne(x => x.Id == id);
			if (!string.IsNullOrEmpty(testimonial.AvatarUrl))
			{
				var avatarDeletionResult = await _contentService.DeletePublicImageAsync(testimonial.AvatarUrl);
			}
			await _unitOfWork.GetRepositoryAsync<Testimonial>().Delete(id);
			return OperationResult.Success();
		}

		public async Task<OperationResult> Get(string id)
		{
			var testimonial = await _unitOfWork.GetRepositoryAsync<Testimonial>().GetOne(x => x.Id == id);
			return OperationResult.Success(null, _mapper.Map<TestimonialViewModel>(testimonial));
		}

		public async Task<OperationResult> GetByContributionId(string contributionId)
		{
			var testimonials = await _unitOfWork.GetRepositoryAsync<Testimonial>().Get(x => x.ContributionId == contributionId);
			return OperationResult.Success(null, _mapper.Map<List<TestimonialViewModel>>(testimonials));
		}

		public async Task<OperationResult> Insert(TestimonialViewModel model)
		{
			var testimonial = _mapper.Map<Testimonial>(model);
			await _unitOfWork.GetRepositoryAsync<Testimonial>().Insert(testimonial);
			return OperationResult.Success(null, _mapper.Map<TestimonialViewModel>(testimonial));
		}

		public async Task<OperationResult> Update(string id, TestimonialViewModel model)
		{
			var testimonial = _mapper.Map<Testimonial>(model);
			await _unitOfWork.GetRepositoryAsync<Testimonial>().Update(id, testimonial);
			return OperationResult.Success(null, _mapper.Map<TestimonialViewModel>(testimonial));
		}
	}
}
