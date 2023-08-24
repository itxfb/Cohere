using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cohere.Entity.Entities.ActiveCampaign
{
    public class ActiveCampaignCustomFields
    {
        [JsonPropertyName("customFieldId")]
        public int CustomFieldId { get; set; }

        [JsonPropertyName("fieldValue")]
        public dynamic FieldValue { get; set; }

        [JsonPropertyName("fieldCurrency")]
        public string FieldCurrency { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }
    }

    public class CustomFieldMeta
	{
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("fieldLabel")]
        public string FieldLabel { get; set; }
    }
    public class ActiveCampaignDealCustomFieldMeta : CustomFieldMeta
    {
        
    }

    public class ActiveCampaignAccountCustomFieldMeta : CustomFieldMeta
    {

    }

	public class ActiveCampaignDealCustomFieldData
	{
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("dealCustomFieldMetumId")]
        public string DealCustomFieldMetumId { get; set; }

        [JsonPropertyName("dealId")]
        public string DealId { get; set; }

        [JsonPropertyName("customFieldId")]
        public string CustomFieldId { get; set; }

        [JsonPropertyName("createdTimestamp")]
        public string CreatedTimestamp { get; set; }

        [JsonPropertyName("updatedTimestamp")]
        public string UpdatedTimestamp { get; set; }

        [JsonPropertyName("fieldValue")]
        public string FieldValue { get; set; }
    }

    public class ActiveCampaignDealCustomFieldDataUpdate
	{
        [JsonPropertyName("fieldValue")]
        public string FieldValue { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("accountCustomFieldMetumId")]
        public string AccountCustomFieldMetumId { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }

        [JsonPropertyName("customFieldId")]
        public string CustomFieldId { get; set; }

        [JsonPropertyName("createdTimestamp")]
        public string CreatedTimestamp { get; set; }

        [JsonPropertyName("updatedTimestamp")]
        public string UpdatedTimestamp { get; set; }

        [JsonPropertyName("fieldValue")]
        public string FieldValue { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldDataUpdate
    {
        [JsonPropertyName("fieldValue")]
        public string FieldValue { get; set; }
    }

    public class ActiveCampaignDealCustomFieldMetaRequest
    {
        [JsonPropertyName("dealCustomFieldMeta")]
        public IEnumerable<ActiveCampaignDealCustomFieldMeta> DealCustomFieldMeta { get; set; }
    }

    public class ActiveCampaignDealCustomFieldDataRequest
    {
        [JsonPropertyName("dealCustomFieldDatum")]
        public ActiveCampaignDealCustomFieldData DealCustomFieldDatum { get; set; }
    }
    public class ActiveCampaignAccountCustomFieldDataRequest
    {
        [JsonPropertyName("accountCustomFieldDatum")]
        public ActiveCampaignAccountCustomFieldData AccountCustomFieldDatum { get; set; }
    }

    public class ActiveCampaignDealCustomFieldDataUpdateRequest
    {
        [JsonPropertyName("dealCustomFieldDatum")]
        public ActiveCampaignDealCustomFieldDataUpdate DealCustomFieldDatum { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldDataUpdateRequest
    {
        [JsonPropertyName("accountCustomFieldDatum")]
        public ActiveCampaignAccountCustomFieldDataUpdate AccountCustomFieldDatum { get; set; }
    }

    public class ActiveCampaignDealCustomFieldDatumRequest
    {
        [JsonPropertyName("dealCustomFieldData")]
        public IEnumerable<ActiveCampaignDealCustomFieldData> DealCustomFieldData { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldDatumRequest
    {
        [JsonPropertyName("accountCustomFieldData")]
        public IEnumerable<ActiveCampaignAccountCustomFieldData> AccountCustomFieldData { get; set; }
    }

    public class ActiveCampaignDealCustomFieldDataResponse
    {
        [JsonPropertyName("dealCustomFieldDatum")]
        public ActiveCampaignDealCustomFieldData dealCustomFieldDatum { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldDataResponse
    {
        [JsonPropertyName("accountCustomFieldDatum")]
        public ActiveCampaignAccountCustomFieldData accountCustomFieldDatum { get; set; }
    }


    public class ActiveCampaignAccountCustomFieldMetaResponse
    {
        [JsonPropertyName("accountCustomFieldMeta")]
        public IEnumerable<ActiveCampaignAccountCustomFieldMeta> AccountCustomFieldMeta { get; set; }
    }

    public class ActiveCampaignAccountCustomFieldMetaRequest
    {
        [JsonPropertyName("accountCustomFieldMeta")]
        public IEnumerable<ActiveCampaignAccountCustomFieldMeta> AccountCustomFieldMeta { get; set; }
    }

    public class ActiveCampaignDealCustomFieldMetaResponse
    {
        [JsonPropertyName("dealCustomFieldMeta")]
        public IEnumerable<ActiveCampaignDealCustomFieldMeta> DealCustomFieldMeta { get; set; }
    }

    public class CohereDealCustomFieldPaidTear
    {
        public string Launch = "Launch";
        public string ImpactMonthly = "Impact Monthly";
        public string ImpactAnnual = "Impact Annual";
        public string ImpactSixMonth = "Impact Six Month";
        public string ScaleMonthly = "Scale Monthly";
        public string ScaleAnnual = "Scale Annual";
        public string AccountCanceled = "Account Cancelled";
    }
}
