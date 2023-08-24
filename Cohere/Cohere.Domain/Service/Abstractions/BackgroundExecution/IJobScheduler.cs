using System;

using Cohere.Domain.Service.BackgroundExecution;

namespace Cohere.Domain.Service.Abstractions.BackgroundExecution
{
    public interface IJobScheduler
    {
        IDelayExecutionSettings Settings { get; }

        string ScheduleJob<TJob>(TimeSpan delay, params object[] args)
            where TJob : IJob;
        string UpdateScheduleJob<TJob>(string JobId, TimeSpan delay, params object[] args)
            where TJob : IJob;
        bool DeleteScheduleJob<TJob>(string JobId)
            where TJob : IJob;
        void Enqueue<TJob>(params object[] args)
            where TJob : IJob;

        void EnqueueAdync<TJobAsync>(params object[] args)
            where TJobAsync : IJobAsync;
    }
}
