using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.User
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PreferenceLevels
    {
        NotInterested = 0,

        Interested = 1,

        VeryInterested = 2
    }
}
