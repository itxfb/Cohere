using System;
using System.Text.Json.Serialization;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasEvent
    {
        [JsonPropertyName("date")]
        public long UnixDate { get; set; }

        [JsonIgnore]
        public DateTimeOffset Date { get => DateTimeOffset.FromUnixTimeSeconds(UnixDate); }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("object_data")]
        public NylasEventData Data { get; set; }
    }
}