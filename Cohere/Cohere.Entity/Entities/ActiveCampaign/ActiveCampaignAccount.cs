using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignAccount
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("accountUrl")]
        public string AccountUrl { get; set; }

        [JsonPropertyName("fields")]
        public List<ActiveCampaignCustomFields> Fields { get; set; } = new List<ActiveCampaignCustomFields>();

        [JsonPropertyName("createdTimestamp")]
        public string CreatedTimestamp { get; set; }

        [JsonPropertyName("updatedTimestamp")]
        public string UpdatedTimestamp { get; set; }

        [JsonPropertyName("links")]
        public List<string> Links { get; set; } = new List<string>();
    }
    public class ActiveCampaignAccountRequest
    {
        [JsonPropertyName("account")]
        public ActiveCampaignAccount Account { get; set; }
    }

    public class ActiveCampaignAccountResponse
    {
        [JsonPropertyName("account")]
        public ActiveCampaignAccount Account { get; set; }
    }

    public enum CohereAccountType
    {
        [Display(Name = "Coach Account Activated")]
        CoachAccountActivated = 1,
        [Display(Name = "Client Account Activated")]
        ClientAccountActivated = 2,
        [Display(Name = "Coach And Client Account Activated")]
        CoachAndClientAccountActivated = 3
    }

    public class AccountCustomFieldLabel
    {
        public string CreatedCohereAccount = "Created Cohere Account";
        public string CohereAccountType = "CohereAccountType";
    }

}
