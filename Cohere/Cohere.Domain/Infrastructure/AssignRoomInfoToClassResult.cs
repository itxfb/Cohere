namespace Cohere.Domain.Infrastructure
{
    public class AssignRoomInfoToClassResult
    {
        public bool Succeeded { get; }

        public string Message { get; }

        public string Payload { get; }

        public AssignRoomInfoToClassResult(bool succeeded, string message, string payload = null)
        {
            Succeeded = succeeded;
            Message = message;
            Payload = payload;
        }
    }
}
