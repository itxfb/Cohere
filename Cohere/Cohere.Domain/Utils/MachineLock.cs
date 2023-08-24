using System;
using System.Threading;

namespace Cohere.Domain.Utils
{
    public class MachineLock : IDisposable
    {
        private Mutex _mutex;
        private readonly bool _owned;

        public MachineLock(string name, TimeSpan timeToWait, int numberOfRetry)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Lock name must be not empty");
            }

            try
            {
                _mutex = new Mutex(true, name, out _owned);

                for (var i = 1; i < numberOfRetry; i++)
                {
                    if (!_owned)
                    {
                        _owned = _mutex.WaitOne(timeToWait);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public MachineLock(string name, TimeSpan timeToWait)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Lock name must be not empty");
            }

            try
            {
                _mutex = new Mutex(true, name, out _owned);
                if (_owned)
                {
                    return;
                }

                _owned = _mutex.WaitOne(timeToWait);
                if (!_owned)
                {
                    throw new MachineLockTimeoutException(name, "Unable to obtain lock within timeout");
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static bool IsLocked(string name)
        {
            var isLocked = true;
            try
            {
                using (Create(name, TimeSpan.FromMilliseconds(1)))
                {
                    isLocked = false;
                }
            }
            catch (MachineLockTimeoutException)
            {
            }

            return isLocked;
        }

        public static IDisposable Create(string name, TimeSpan timeSpan)
        {
            return new MachineLock(name, timeSpan);
        }

        public static IDisposable Create(string name, TimeSpan timeSpan, int numberOfRetry)
        {
            return new MachineLock(name, timeSpan, numberOfRetry);
        }

        public void Dispose()
        {
            if (_owned)
            {
                _mutex.ReleaseMutex();
            }

            _mutex = null;
        }
    }
}
