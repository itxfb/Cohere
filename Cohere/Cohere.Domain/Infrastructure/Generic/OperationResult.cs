using System.Collections.Generic;
using FluentValidation.Results;
using Newtonsoft.Json;

namespace Cohere.Domain.Infrastructure.Generic
{
    public class OperationResult<T> : OperationResult
        where T : class
    {
        public new T Payload => (T)base.Payload;

        public OperationResult(bool succeeded, string message, T payload = null)
            : base(succeeded, message, payload)
        {
        }

        public OperationResult(T payload = null)
            : base(true, null, payload)
        {
        }

        public static OperationResult<T> Success(string message, T payload = null)
        {
            return new OperationResult<T>(true, message, payload);
        }

        public static OperationResult<T> Success(T payload = null)
        {
            return new OperationResult<T>(true, string.Empty, payload);
        }

        public static OperationResult<T> Failure(string message, T payload = null)
        {
            return new OperationResult<T>(false, message, payload);
        }

        public static OperationResult<T> ValidationError(IList<ValidationFailure> errors)
        {
            return new OperationResult<T>(false, JsonConvert.SerializeObject(errors));
        }

        public static OperationResult<T> Forbid(string message)
        {
            return new OperationResult<T>(false, message) { Forbidden = true };
        }

        public static new OperationResult<T> Success()
        {
            return Success(string.Empty);
        }
    }
}