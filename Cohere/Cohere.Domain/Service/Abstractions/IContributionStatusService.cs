using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IContributionStatusService
    {
        Task ExposeContributionsToReviewAsync(string userId);
    }
}