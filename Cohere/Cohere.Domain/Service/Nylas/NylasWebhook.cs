using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasWebhook
    {
        [JsonPropertyName("deltas")]
        public List<NylasEvent> Deltas { get; set; }
    }
}