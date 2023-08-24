using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.Contribution
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContributionGenders
    {
        NoRequirement = 0,

        Male = 1,

        Female = 2,

        NonBinary = 3
    }
}
