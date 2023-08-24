using System.Collections.Generic;

namespace Cohere.Api.Models.Responses
{
    public class BaseResponse
    {
        public BaseResponse()
        {
        }

        public BaseResponse(string reason)
        {
            Status.Reason = reason;
        }

        public BaseResponse(string reason, string message)
        {
            Status.Reason = reason;
            Status.Messages = new List<string> { message };
        }

        public BaseResponse(IEnumerable<string> messages)
        {
            Status.Reason = "Bad Request";
            Status.Messages = messages;
        }

        public BaseResponse(string reason, IEnumerable<string> messages)
        {
            Status.Reason = reason;
            Status.Messages = messages;
        }

        public StatusResponse Status { get; set; } = new StatusResponse();
    }
}