using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions.BackgroundExecution
{
    public interface IJobAsync
    {
        Task ExecuteAsync(params object[] args);
    }
}
