using System.Text.Json.Serialization;

namespace Cohere.Domain.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ToggleStatus
    {
        Started = 1,

        Stopped = 2
    }
}