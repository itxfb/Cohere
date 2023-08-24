using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.Contribution
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContributionStatuses
    {
        Unfinished = 0,

        InSandbox = 1,

        InReview = 2,

        Revised = 3,

        Approved = 4,

        Rejected = 5,

        ChangeRequired = 6,

        Completed = 7,

        Draft = 8
    }
}
