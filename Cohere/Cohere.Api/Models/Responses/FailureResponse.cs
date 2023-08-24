using System.Collections.Generic;

namespace Cohere.Api.Models.Responses
{
    public class FailureResponse : BaseResponse
    {
        public FailureResponse(string reason) : base(reason)
        {
        }

        public FailureResponse(string reason, string message) : base(reason, message)
        {
        }

        public FailureResponse(IEnumerable<string> messages) : base(messages)
        {
        }

        public FailureResponse(string reason, IEnumerable<string> messages) : base(reason, messages)
        {
        }
    }
}