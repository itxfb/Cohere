using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Roles
    {
        SuperAdmin = 1,

        Admin = 2,

        Cohealer = 3,

        CohealerAssistant = 4,

        Client = 5
    }
}