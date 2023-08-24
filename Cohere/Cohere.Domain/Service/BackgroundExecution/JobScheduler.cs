using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class JobScheduler : IJobScheduler
    {
        private readonly IServiceProvider _serviceProvider;

        public IDelayExecutionSettings Settings { get; }

        public JobScheduler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Settings = _serviceProvider.GetService<IOptions<DelayExecutionSettings>>().Value;
        }

        public string ScheduleJob<TJob>(TimeSpan delay, params object[] args)
            where TJob : IJob
        {
            var job = _serviceProvider.GetService<TJob>();

            string JobId = BackgroundJob.Schedule(() => job.Execute(args), delay);

            return JobId;
        }
        public string UpdateScheduleJob<TJob>(string JobId, TimeSpan delay, params object[] args)
            where TJob : IJob
        {
            string newJobId = "";
            var job = _serviceProvider.GetService<TJob>();
            if (BackgroundJob.Delete(JobId))
            {
                newJobId = BackgroundJob.Schedule(() => job.Execute(args), delay);
            }

            return newJobId;
        }
         public bool DeleteScheduleJob<TJob>(string JobId)
            where TJob : IJob
        {
            if (BackgroundJob.Delete(JobId))
            {
                return true;
            }

            return false;
        }

        public void Enqueue<TJob>(params object[] args)
            where TJob : IJob
        {
            BackgroundJob.Enqueue<TJob>(job => job.Execute(args));
        }

        public void EnqueueAdync<TJobAsync>(params object[] args)
            where TJobAsync : IJobAsync
        {
            BackgroundJob.Enqueue<TJobAsync>(job => job.ExecuteAsync(args));
        }
    }
}
