using System.Text.Json.Serialization;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasEventData
    {
        [JsonPropertyName("namespace_id")]
        public string NamespaceId { get; set; }

        [JsonPropertyName("account_id")]
        public string AccountId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}