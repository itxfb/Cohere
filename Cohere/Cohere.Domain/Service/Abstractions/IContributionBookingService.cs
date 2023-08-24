using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Entity.Entities.Contrib;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContributionBookingService
    {
        OperationResult BookSessionTimeAsync(List<BookSessionTimeViewModel> bookModels, string requesterAccountId, int logId=0);

        Task<OperationResult> RevokeBookingOfSessionTimeAsync(BookSessionTimeViewModel bookModel, string requesterAccountId, SessionBasedContribution existedCourse=null);
    }
}
