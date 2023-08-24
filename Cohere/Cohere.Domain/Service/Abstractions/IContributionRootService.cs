using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.Enums.Contribution;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContributionRootService
    {
        Task<ContributionBase> GetOne(string contributionId);

        Task<IEnumerable<ContributionBase>> Get(Expression<Func<ContributionBase, bool>> predicate);
        Task<IEnumerable<ContributionBase>> GetSkipTake(Expression<Func<ContributionBase, bool>> predicate, int skip, int take);
        Task<IEnumerable<ContributionBase>> GetSkipTakeWithSort(Expression<Func<ContributionBase, bool>> predicate, int skip, int take, OrderByEnum orderByEnum);
        Task<int> GetCount(Expression<Func<ContributionBase, bool>> predicate);

        Task<ContributionBase> GetOne(Expression<Func<ContributionBase, bool>> predicate);

        Task<IEnumerable<AvailabilityTime>> GetAvailabilityTimesForCoach(string contributionId, int offset, OneToOneSessionDataUi schedulingCriteria = default, bool timesInUtc = false);

        Task<IEnumerable<CohealerContributionTimeRangeViewModel>> GetCohealerContributionsTimeRangesForCohealer(string cohealerAccountId, bool timesInUtc = false);

        Task<IEnumerable<AvailabilityTime>> CalculateSlots(string coachAccountId, OneToOneSessionDataUi schedulingCriteria, bool timesInUtc = false);

        Task<IEnumerable<AvailabilityTime>> GetAvailabilityTimesForClient(string contributionId, string clientAccountId, int offset, string timezoneId, bool timesInUtc = false, bool withTimeZoneId = false);
    }
}
