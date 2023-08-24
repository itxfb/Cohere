namespace Cohere.Domain.Service.Abstractions.BackgroundExecution
{
    public interface IJob
    {
        void Execute(params object[] args);
    }
}
