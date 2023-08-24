using System.Collections.Generic;

namespace Cohere.Api.Models.Responses
{
    public class StatusResponse
    {
        public string Reason { get; set; }

        public IEnumerable<string> Messages { get; set; } = new List<string>();
    }
}