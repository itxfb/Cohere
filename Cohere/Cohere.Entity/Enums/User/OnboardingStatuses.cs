using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.User
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OnboardingStatuses
    {
        Registered = 1,

        InfoAdded = 2,

        IdVerified = 3,

        Blocked = 4,

        Certified = 5
    }
}
