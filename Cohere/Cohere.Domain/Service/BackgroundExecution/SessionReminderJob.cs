using AutoMapper;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class SessionReminderJob : IHostedService, IDisposable
    {
        private Timer _timer;
        private bool _disposed;
        private Task doWorkTask;
        private readonly INotificationService _notificationService;
        private readonly NylasService _nylasService;
        private TimeSpan startTime = new TimeSpan(13, 0, 0);
        private string timeZoneToCalculateTomorrowStart = "America/New_York";

        public SessionReminderJob(INotificationService notificationService, NylasService nylasService)
        {
            _notificationService = notificationService;
            _nylasService = nylasService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            var todayUtc = DateTime.UtcNow;
            TimeSpan delay;
            if (todayUtc.TimeOfDay < startTime)
            {
                delay = startTime - todayUtc.TimeOfDay;
            }
            else
            {
                delay = startTime + new TimeSpan(24, 0, 0) - todayUtc.TimeOfDay;
            }

            _timer = new Timer(ExecuteTask, null, delay, TimeSpan.FromDays(1));

            return Task.CompletedTask;
        }

        private void ExecuteTask(object state)
        {
            doWorkTask = DoWork();
        }

        private async Task DoWork()
        {
            var dateTimeJobFires = DateTime.UtcNow;

            var timeReminderFiresZoned = DateTimeHelper.GetZonedDateTimeFromUtc(dateTimeJobFires, timeZoneToCalculateTomorrowStart);

            var tomorrowStartMomentZoned = timeReminderFiresZoned.AddDays(1).Date;

            var tomorrowStartMomentUtc = DateTimeHelper.GetUtcTimeFromZoned(tomorrowStartMomentZoned, timeZoneToCalculateTomorrowStart);
            var dayAfterTomorrowStartMomentUtc = tomorrowStartMomentUtc.AddHours(24);

            await _notificationService.SendSessionReminders(tomorrowStartMomentZoned, dayAfterTomorrowStartMomentUtc, false);

            //Remove Nylas Account of Inactive users
            await _nylasService.RemoveNylasAccountForInActiveUsersAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _timer?.Dispose();
            }

            _disposed = true;
        }
    }
}
