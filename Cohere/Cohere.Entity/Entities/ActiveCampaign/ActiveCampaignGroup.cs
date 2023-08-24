using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }
    }
    public class ActiveCampaignGroupsRequest
    {
        [JsonPropertyName("dealGroups")]
        public IEnumerable<ActiveCampaignGroup> DealGroups { get; set; }
    }

    public class ActiveCampaignGroupsResponse
    {
        [JsonPropertyName("dealGroups")]
        public IEnumerable<ActiveCampaignGroup> DealGroups { get; set; }
    }
}
