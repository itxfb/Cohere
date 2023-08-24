using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.User
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PhoneTypes
    {
        Home = 1,

        Work = 2,

        Mobile = 3
    }
}