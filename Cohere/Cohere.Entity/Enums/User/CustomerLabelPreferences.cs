using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.User
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CustomerLabelPreferences
    {
        Clients = 1,

        Students = 2,

        Customers = 3
    }
}
