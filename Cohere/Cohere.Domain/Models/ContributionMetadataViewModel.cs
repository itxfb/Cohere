using System.Text.Json.Serialization;

namespace Cohere.Domain.Models
{
    public class ContributionMetadataViewModel
    {
        [JsonPropertyName("SERVER_META_TITLE")]
        public string Title { get; set; } = "Cohere.live";

        [JsonPropertyName("SERVER_META_DESCRIPTION")]
        public string Description { get; set; } = "One place to sell, deliver, and scale your online services";

        [JsonPropertyName("SERVER_META_IMAGE")]
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("SERVER_META_URL")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("SERVER_META_TYPE")]
        public string Type { get; set; } = "website";
    }
}
