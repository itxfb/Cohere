using System;

namespace Cohere.Domain.Utils
{
    public class MachineLockTimeoutException : Exception
    {
        public readonly string LockName;

        public MachineLockTimeoutException(string lockName)
        {
            LockName = lockName;
        }

        public MachineLockTimeoutException(string lockName, string message)
            : base(message)
        {
            LockName = lockName;
        }

        public MachineLockTimeoutException(string lockName, string message, Exception inner)
            : base(message, inner)
        {
            LockName = lockName;
        }
    }
}