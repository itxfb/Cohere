using System;

namespace Cohere.Domain.Infrastructure
{
    public class AccessDeniedException : Exception
    {
        public AccessDeniedException(string message)
            : base(message)
        {
        }
    }
}
