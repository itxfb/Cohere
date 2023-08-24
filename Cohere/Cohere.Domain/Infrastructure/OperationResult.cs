namespace Cohere.Domain.Infrastructure
{
    public class OperationResult
    {
        public static OperationResult Success() => new OperationResult(true, null);

        public static OperationResult Success(string message, object payload = null) => new OperationResult(true, message, payload);

        public static OperationResult Failure(string message, object payload = null) => new OperationResult(false, message, payload);

        public static OperationResult Forbid(string message, object payload = null) => new OperationResult(false, message, payload) { Forbidden = true };

        public bool Succeeded { get; }

        public bool Failed => !Succeeded;

        public bool Forbidden { get; set; }

        public string Message { get; }

        public object Payload { get; }

        public OperationResult(bool succeeded, string message, object payload = null)
        {
            Succeeded = succeeded;
            Message = message;
            Payload = payload;
        }
    }
}
