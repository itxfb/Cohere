using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignStage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("group")]
        public string Group { get; set; }

    }
    public class ActiveCampaignStagesRequest
    {
        [JsonPropertyName("dealStages")]
        public IEnumerable<ActiveCampaignStage> DealStages { get; set; }
    }

    public class ActiveCampaignStagesResponse
    {
        [JsonPropertyName("dealStages")]
        public IEnumerable<ActiveCampaignStage> DealStages { get; set; }
    }
}
