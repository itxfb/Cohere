using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.Account
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignupTypes
    {
        NONE = 0,
        SIGNUP_A = 1,
        SIGNUP_B = 2,
    }
}