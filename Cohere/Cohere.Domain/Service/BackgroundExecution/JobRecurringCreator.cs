using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class JobRecurringCreator : IJobRecurringCreator
    {
        private readonly IServiceProvider _serviceProvider;

        public JobRecurringCreator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void CreateDailyRecurringJob<TJob>(int firesHours, int firesMinutes, params object[] args)
            where TJob : IJob
        {
            var job = _serviceProvider.GetService<TJob>();
            RecurringJob.AddOrUpdate(() => job.Execute(args), Cron.Daily(firesHours, firesMinutes));
        }

        public void CreateHourlyRecurringJob<TJob>(params object[] args)
            where TJob : IJob
        {
            var job = _serviceProvider.GetService<TJob>();
            RecurringJob.AddOrUpdate(() => job.Execute(args), Cron.Hourly);
        }
        public void CreateMinutelyRecurringJob<TJob>(params object[] args)
            where TJob : IJob
        {
            var job = _serviceProvider.GetService<TJob>();
            RecurringJob.AddOrUpdate(() => job.Execute(args), "*/5 * * * *");
        }
    }
}
