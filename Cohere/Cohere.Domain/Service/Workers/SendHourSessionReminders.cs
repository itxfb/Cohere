using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Workers
{
	public class SendHourSessionReminders : IHostedService, IDisposable
	{
        private Timer _timer;
        private bool _disposed;
        private Task doWorkTask;
        private readonly INotificationService _notificationService;

        public SendHourSessionReminders(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(5));

            return Task.CompletedTask;
        }

        private void ExecuteTask(object state)
        {
            doWorkTask = DoWork();
        }

        private async Task DoWork()
        {
            var dateTimeJobFires = DateTime.UtcNow;

            var startTime = dateTimeJobFires.AddHours(1);
            var endime = startTime.AddMinutes(5);

            await _notificationService.SendSessionReminders(startTime, endime, true);
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
