using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignContact
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("fieldValues")]
        public List<ActiveCampaignContactFields> FieldValues { get; set; } = new List<ActiveCampaignContactFields>();
    }

    public class ActiveCampaignAccountContact
	{
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("account")]
        public string Account { get; set; }

        [JsonPropertyName("contact")]
        public string Contact { get; set; }

    }

    public class ActiveCampaignAccountContactRequest
    {
        [JsonPropertyName("accountContact")]
        public ActiveCampaignAccountContact AccountContact { get; set; }
    }

    public class ActiveCampaignAccountContactsRequest
    {
        [JsonPropertyName("accountContacts")]
        public IEnumerable<ActiveCampaignAccountContact> AccountContacts { get; set; }
    }

    public class ActiveCampaignContactRequest
    {
        [JsonPropertyName("contact")]
        public ActiveCampaignContact Contact { get; set; }
    }

    public class ActiveCampaignContactsRequest
    {
        [JsonPropertyName("contacts")]
        public IEnumerable<ActiveCampaignContact> Contacts { get; set; }
    }

    public class ActiveCampaignContactResponse
    {
        [JsonPropertyName("contact")]
        public ActiveCampaignContactResp Contact { get; set; }
        public List<ActiveCampaignContactFields> FieldValues { get; set; }
    }

    public class ActiveCampaignContactResp
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("fieldValues")]
        public List<string> FieldValues { get; set; }

        [JsonPropertyName("cdate")]
        public string CreatedDate { get; set; }

        [JsonPropertyName("udate")]
        public string UpdatedDate { get; set; }

        [JsonPropertyName("owner")]
        public string Owner { get; set; }

        [JsonPropertyName("links")]
        public object Links { get; set; }
    }

    public class ActiveCampaignContactFields
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
     
        [JsonPropertyName("field")]
        public string Field { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("cdate")]
        public string CreatedDate { get; set; }

        [JsonPropertyName("udate")]
        public string UpdatedDate { get; set; }

        [JsonPropertyName("owner")]
        public string Owner { get; set; }

        [JsonPropertyName("links")]
        public object Links { get; set; }

        [JsonPropertyName("contact")]
        public string Contact { get; set; }
    }
}
