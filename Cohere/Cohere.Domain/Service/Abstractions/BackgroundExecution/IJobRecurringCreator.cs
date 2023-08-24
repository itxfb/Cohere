namespace Cohere.Domain.Service.Abstractions.BackgroundExecution
{
    public interface IJobRecurringCreator
    {
        void CreateDailyRecurringJob<TJob>(int firesHours, int firesMinutes, params object[] args)
            where TJob : IJob;

        void CreateHourlyRecurringJob<TJob>(params object[] args)
            where TJob : IJob;
        void CreateMinutelyRecurringJob<TJob>(params object[] args)
            where TJob : IJob;
    }
}
