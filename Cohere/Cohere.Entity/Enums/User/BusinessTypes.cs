using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.User
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BusinessTypes
    {
        Coaching = 1,

        Teaching = 2,

        Mentoring = 3,

        Consulting = 4,

        Other = 5
    }
}
