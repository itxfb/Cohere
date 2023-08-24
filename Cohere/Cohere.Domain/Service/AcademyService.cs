using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AutoMapper;

using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;

namespace Cohere.Domain.Service
{
    public class AcademyService : IAcademyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AcademyService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OperationResult<List<AcademyContributionPreviewViewModel>>>
            GetContributionBundledWithPaidTierProductAsync()
        {
            var bundleInfos = await _unitOfWork.GetRepositoryAsync<BundleInfo>()
                .Get(e => e.BundleParentType == BundleParentType.PaidTierProduct
                          || e.BundleParentType == BundleParentType.PaidTierOption);

            var contributionIds = bundleInfos.Select(e => e.ItemId).Distinct().ToArray();

            var contributions = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .Get(e => contributionIds.Contains(e.Id));

            var authorIds = contributions.Select(e => e.UserId).Distinct().ToArray();

            var authors = await _unitOfWork.GetRepositoryAsync<User>().Get(e => authorIds.Contains(e.Id));

            var serviceProviderNames = authors.ToDictionary(k => k.Id, v => $"{v.FirstName} {v.LastName}");

            var serviceProviderAvatars = authors.ToDictionary(e => e.Id, v => v.AvatarUrl);

            var contributionPreviewViewModels = _mapper.Map<List<AcademyContributionPreviewViewModel>>(contributions);

            foreach (var contributionPreviewViewModel in contributionPreviewViewModels)
            {
                contributionPreviewViewModel.ServiceProviderName =
                    serviceProviderNames.GetValueOrDefault(contributionPreviewViewModel.UserId, "deleted user");

                contributionPreviewViewModel.AvatarUrl =
                    serviceProviderAvatars.GetValueOrDefault(contributionPreviewViewModel.UserId);
            }

            return OperationResult<List<AcademyContributionPreviewViewModel>>.Success(contributionPreviewViewModels);
        }
    }
}