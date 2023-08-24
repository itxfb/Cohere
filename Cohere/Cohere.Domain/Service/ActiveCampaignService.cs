using System.Collections.Generic;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities.ActiveCampaign;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Cohere.Domain.Models.Account;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Linq;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums.Contribution;
using System;

namespace Cohere.Domain.Service
{
    public interface IActiveCampaignService
    {
        Task<ActiveCampaignAccountResponse> CreateAccountAsync(ActiveCampaignAccount account);
        Task<ActiveCampaignAccountRequest> GetAccountAsync(string id);
        Task<ActiveCampaignAccountResponse> UpdateAccountAsync(string id, ActiveCampaignAccount account);
        Task<ActiveCampaignDealResponse> CreateDealAsync(ActiveCampaignDeal deal);
		Task<ActiveCampaignDealResponse> UpdateDealAsync(string id, ActiveCampaignDeal deal);
        Task<ActiveCampaignDeal> GetLatestDealAsync(string email);
        Task<ActiveCampaignContactResponse> CreateContactAsync(ActiveCampaignContact contact);
        Task<ActiveCampaignContactResponse> UpdateContactAsync(string id, ActiveCampaignContact contact);
        Task<ActiveCampaignContact> GetContactByEmailAsync(string email);
        Task<ActiveCampaignStage> GetStageByNameAndPipelineName(string stageName, string pipelineName);
        Task<ActiveCampaignDealCustomFieldMeta> GetActiveCampaignDealCustomFieldMetaByLabel(string label);
        Task<ActiveCampaignDealCustomFieldData> GetActiveCampaignDealCustomFieldData(string dealId, string fieldId);
        Task<ActiveCampaignDealCustomFieldDataResponse> CreateDealCustomFieldDataAsync(ActiveCampaignDealCustomFieldData dealCustomFieldData);
        Task<ActiveCampaignDealCustomFieldDataResponse> UpdateDealCustomFieldDataAsync(string id, ActiveCampaignDealCustomFieldDataUpdate dealCustomFieldData);
        Task<ActiveCampaignAccountCustomFieldMeta> GetActiveCampaignAccountCustomFieldMetaByLabel(string label);
        Task<ActiveCampaignAccountContact> GetActiveCampaignAccountContactAssociationByContactId(string contactId);
        Task<ActiveCampaignAccountContactRequest> CreateAccountContactAssociationAsync(ActiveCampaignAccountContact accountContact);
        Task<ActiveCampaignAccountCustomFieldData> GetActiveCampaignAccountCustomFieldData(string accountId, string fieldId);
        Task<ActiveCampaignAccountCustomFieldDataResponse> CreateAccountCustomFieldDataAsync(ActiveCampaignAccountCustomFieldData accountCustomFieldData);
        Task<ActiveCampaignAccountCustomFieldDataResponse> UpdateAccountCustomFieldDataAsync(string id, ActiveCampaignAccountCustomFieldDataUpdate accountCustomFieldData);
        Task<bool> IsContactExistsAndHaveDeal(string email);


        void SendActiveCampaignEvents(ActiveCampaignContact contract, string cohereAccountType, string createdCohereAccount, string accountName);
        void SendActiveCampaignEvents(ActiveCampaignDeal deal, ActiveCampaignDealCustomFieldOptions options);
        string PaidTearOptionToActiveCampaignDealCustomFieldValue(PaidTierOption desiredPaidTier, PaidTierOptionPeriods newPaymentPeriod);
    }
    public class ActiveCampaignService : IActiveCampaignService
    {
		private const string Deals = "deals";
        private const string Accounts = "accounts";
        private const string Contacts = "contacts";
        private const string DealStages = "dealStages";
        private const string Pipelines = "dealGroups";
        private const string DealCustomFieldMeta = "dealCustomFieldMeta";
        private const string DealCustomFieldData = "dealCustomFieldData";
        private const string AccountCustomFieldMeta = "accountCustomFieldMeta";
        private const string AccountCustomFieldData = "accountCustomFieldData";
        private const string AccountContacts = "accountContacts";
        private readonly IActiveCampaignClient _activeCampaignClient;
        private readonly IAmazonSQS _amazonSqs;
        private readonly string _activeCampaignQueueUrl;


        public ActiveCampaignService(IActiveCampaignClient activeCampaignClient, 
            IAmazonSQS amazonSqs,
            IOptions<SqsSettings> sqsOptions)
        {
            _activeCampaignClient = activeCampaignClient;
            _amazonSqs = amazonSqs;
            _activeCampaignQueueUrl = sqsOptions.Value.ActiveCampaignQueueUrl;
        }

        public async Task<ActiveCampaignAccountResponse> CreateAccountAsync(ActiveCampaignAccount account)
        {
            var payload = new ActiveCampaignAccountRequest()
            {
                Account = account
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignAccountRequest, ActiveCampaignAccountResponse>(Accounts, payload);
            return response;
        }

        public async Task<ActiveCampaignAccountRequest> GetAccountAsync(string id)
		{
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignAccountRequest>(Accounts, id);
            return response;
        }

        public async Task<ActiveCampaignAccountResponse> UpdateAccountAsync(string id, ActiveCampaignAccount account)
        {
            var payload = new ActiveCampaignAccountRequest()
            {
                Account = account
            };

            var response = await _activeCampaignClient.PutAsync<ActiveCampaignAccountRequest, ActiveCampaignAccountResponse>(Accounts, id, payload);
            return response;
        }

        public async Task<ActiveCampaignDealResponse> CreateDealAsync(ActiveCampaignDeal deal)
        {
            var payload = new ActiveCampaignDealRequest()
            {
                Deal = deal
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignDealRequest, ActiveCampaignDealResponse>(Deals, payload);
            return response;
        }

        public async Task<ActiveCampaignDealResponse> UpdateDealAsync(string id, ActiveCampaignDeal deal)
        {
            var payload = new ActiveCampaignDealRequest()
            {
                Deal = deal
            };

            var response = await _activeCampaignClient.PutAsync<ActiveCampaignDealRequest, ActiveCampaignDealResponse>(Deals, id, payload);
            return response;
        }

        public async Task<ActiveCampaignContactResponse> CreateContactAsync(ActiveCampaignContact contact)
        {
            var payload = new ActiveCampaignContactRequest()
            {
                Contact = contact
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignContactRequest, ActiveCampaignContactResponse>(Contacts, payload);
            return response;
        }

        public async Task<ActiveCampaignContactResponse> UpdateContactAsync(string id, ActiveCampaignContact contact)
        {
            var payload = new ActiveCampaignContactRequest()
            {
                Contact = contact
            };

            var response = await _activeCampaignClient.PutAsync<ActiveCampaignContactRequest, ActiveCampaignContactResponse>(Contacts, id, payload);
            return response;
        }

        public async Task<ActiveCampaignContact> GetContactByEmailAsync(string email)
        {

            var response = await _activeCampaignClient.GetAsync<ActiveCampaignContactsRequest>(Contacts, email: email);
            return response?.Contacts?.SingleOrDefault(c => c.Email?.ToLower() == email.ToLower());
		}

        public async Task<ActiveCampaignDeal> GetLatestDealAsync(string email)
		{
            var contact = await GetContactByEmailAsync(email);
            if(contact != null)
			{
                var response = await _activeCampaignClient.GetAsync<ActiveCampaignDealsRequest>(Deals, contact: contact.Id, limit: -1);
                return response?.Deals?
                    .Where(d => d.Contact == contact.Id && !string.IsNullOrEmpty(d.cDate))?
                    .OrderByDescending(d => DateTime.Parse(d.cDate))
                    .FirstOrDefault();
			}
            return null;

        }

        public async Task<ActiveCampaignStage> GetStageByNameAndPipelineName(string stageName, string pipelineName)
        {
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignStagesRequest>(DealStages);
			var allPipelines = await _activeCampaignClient.GetAsync<ActiveCampaignGroupsRequest>(Pipelines);
            var pipeline = allPipelines?.DealGroups?.SingleOrDefault(p => p.Title == pipelineName);
            return response?.DealStages?.SingleOrDefault(s => s.Title == stageName && s.Group == pipeline?.Id);
        }

        public async Task<ActiveCampaignDealCustomFieldMeta> GetActiveCampaignDealCustomFieldMetaByLabel(string label)
		{
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignDealCustomFieldMetaRequest>(DealCustomFieldMeta);
            return response?.DealCustomFieldMeta?.SingleOrDefault(s => s.FieldLabel?.ToLower()?.Trim() == label?.ToLower()?.Trim());
        }

        public async Task<ActiveCampaignDealCustomFieldData> GetActiveCampaignDealCustomFieldData(string dealId, string fieldId)
		{
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignDealCustomFieldDatumRequest>(Deals, extraSubSegment1: dealId, extraSubSegment2: DealCustomFieldData);
            return response.DealCustomFieldData.FirstOrDefault(d => d.DealId == dealId && d.CustomFieldId == fieldId);
        }

        public async Task<ActiveCampaignDealCustomFieldDataResponse> UpdateDealCustomFieldDataAsync(string id, ActiveCampaignDealCustomFieldDataUpdate dealCustomFieldData)
		{
            var payload = new ActiveCampaignDealCustomFieldDataUpdateRequest()
            {
                DealCustomFieldDatum = dealCustomFieldData
            };

            var response = await _activeCampaignClient.PutAsync<ActiveCampaignDealCustomFieldDataUpdateRequest, ActiveCampaignDealCustomFieldDataResponse>(DealCustomFieldData, id, payload);
            return response;
        }

        public async Task<ActiveCampaignAccountCustomFieldData> GetActiveCampaignAccountCustomFieldData(string accountId, string fieldId)
        {
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignAccountCustomFieldDatumRequest>(Accounts, extraSubSegment1: accountId, extraSubSegment2: AccountCustomFieldData);
            return response?.AccountCustomFieldData?.FirstOrDefault(d => d.AccountId == accountId && d.CustomFieldId == fieldId);
        }

        public async Task<ActiveCampaignAccountCustomFieldDataResponse> UpdateAccountCustomFieldDataAsync(string id, ActiveCampaignAccountCustomFieldDataUpdate accountCustomFieldData)
        {
            var payload = new ActiveCampaignAccountCustomFieldDataUpdateRequest()
            {
                AccountCustomFieldDatum = accountCustomFieldData
            };

            var response = await _activeCampaignClient.PutAsync<ActiveCampaignAccountCustomFieldDataUpdateRequest, ActiveCampaignAccountCustomFieldDataResponse>(AccountCustomFieldData, id, payload);
            return response;
        }

        public async Task<ActiveCampaignDealCustomFieldDataResponse> CreateDealCustomFieldDataAsync(ActiveCampaignDealCustomFieldData dealCustomFieldData)
        {
            var payload = new ActiveCampaignDealCustomFieldDataRequest()
            {
                DealCustomFieldDatum = dealCustomFieldData
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignDealCustomFieldDataRequest, ActiveCampaignDealCustomFieldDataResponse>(DealCustomFieldData, payload);
            return response;
        }

        public async Task<ActiveCampaignAccountCustomFieldDataResponse> CreateAccountCustomFieldDataAsync(ActiveCampaignAccountCustomFieldData accountCustomFieldData)
        {
            var payload = new ActiveCampaignAccountCustomFieldDataRequest()
            {
                AccountCustomFieldDatum = accountCustomFieldData
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignAccountCustomFieldDataRequest, ActiveCampaignAccountCustomFieldDataResponse>(AccountCustomFieldData, payload);
            return response;
        }

        public async Task<ActiveCampaignAccountCustomFieldMeta> GetActiveCampaignAccountCustomFieldMetaByLabel(string label)
        {
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignAccountCustomFieldMetaRequest>(AccountCustomFieldMeta);
            return response?.AccountCustomFieldMeta?.SingleOrDefault(s => s.FieldLabel?.ToLower()?.Trim() == label?.ToLower()?.Trim());
        }

        public async Task<ActiveCampaignAccountContact> GetActiveCampaignAccountContactAssociationByContactId(string contactId)
		{
            var response = await _activeCampaignClient.GetAsync<ActiveCampaignAccountContactsRequest>(AccountContacts);
            return response?.AccountContacts?.FirstOrDefault(a => a.Contact == contactId);
        }

        public async Task<ActiveCampaignAccountContactRequest> CreateAccountContactAssociationAsync(ActiveCampaignAccountContact accountContact)
		{
            var payload = new ActiveCampaignAccountContactRequest()
            {
                AccountContact = accountContact
            };

            var response = await _activeCampaignClient.PostAsync<ActiveCampaignAccountContactRequest, ActiveCampaignAccountContactRequest>(AccountContacts, payload);
            return response;
        }

        public void SendActiveCampaignEvents(ActiveCampaignContact contact, string cohereAccountType, string createdCohereAccount, string accountName)
        {
            try
            {
                //Send active campaign contract event

                var contactTask = SendActiveCampaignEvent(contact, cohereAccountType: cohereAccountType, createdCohereAccount: createdCohereAccount, 
                    accountName: accountName, contactEmail: contact?.Email);

                Task.WaitAll(contactTask);
            }
            catch(Exception ex)
			{
                Console.WriteLine("err" + ex.GetType());
			}
        }

        public void SendActiveCampaignEvents(ActiveCampaignDeal deal, ActiveCampaignDealCustomFieldOptions options)
        {
            try
            {
                //Send active campaign deal event
                var dealTask = SendActiveCampaignEvent(deal, options: options);
                Task.WaitAll(dealTask);
            }
            catch
			{

			}
        }

        public string PaidTearOptionToActiveCampaignDealCustomFieldValue(PaidTierOption desiredPaidTier, PaidTierOptionPeriods newPaymentPeriod)
        {
            string paidTearOption = null;
            switch (desiredPaidTier.DisplayName)
            {
                case "Launch":
                    paidTearOption = new CohereDealCustomFieldPaidTear().Launch;
                    break;
                case "Impact":
                    switch (newPaymentPeriod)
                    {
                        case PaidTierOptionPeriods.Annually:
                            paidTearOption = new CohereDealCustomFieldPaidTear().ImpactAnnual;
                            break;
                        case PaidTierOptionPeriods.Monthly:
                            paidTearOption = new CohereDealCustomFieldPaidTear().ImpactMonthly;
                            break;
                        case PaidTierOptionPeriods.EverySixMonth:
                            paidTearOption = new CohereDealCustomFieldPaidTear().ImpactSixMonth;
                            break;

                    }
                    break;
                case "Scale":
                    switch (newPaymentPeriod)
                    {
                        case PaidTierOptionPeriods.Annually:
                            paidTearOption = new CohereDealCustomFieldPaidTear().ScaleAnnual;
                            break;
                        case PaidTierOptionPeriods.Monthly:
                            paidTearOption = new CohereDealCustomFieldPaidTear().ScaleMonthly;
                            break;

                    }
                    break;
            }
            return paidTearOption;
        }

        public async Task<bool> IsContactExistsAndHaveDeal(string email)
        {
            var contact = await GetContactByEmailAsync(email);
            if(!string.IsNullOrEmpty(contact?.Email))
            {
                var deal = await GetLatestDealAsync(contact.Email);
                if(deal != null)
                {
                    return true;
                }
            }
            return false;
        }

        private Task SendActiveCampaignEvent<T>(T evnt, ActiveCampaignDealCustomFieldOptions options = null,
                                                        string createdCohereAccount = null, string cohereAccountType = null,
                                                        string contactEmail = null,
                                                        string accountName = null)
        {
            try
            {
                var messageAttributes = new Dictionary<string, MessageAttributeValue>()
                {
                    {"_type", new MessageAttributeValue(){ StringValue = evnt.GetType().ShortDisplayName(), DataType = "String"} }
                };
				if (!string.IsNullOrWhiteSpace(options?.CohereAccountId))
				{
					messageAttributes.Add("_cai", new MessageAttributeValue() { StringValue = options?.CohereAccountId, DataType = "String" });
				}
				if (!string.IsNullOrWhiteSpace(options?.StageName))
                {
                    messageAttributes.Add("_s", new MessageAttributeValue() { StringValue = options?.StageName, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.PipelineName))
                {
                    messageAttributes.Add("_p", new MessageAttributeValue() { StringValue = options?.PipelineName, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.PaidTier))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().PaidTier, new MessageAttributeValue() { StringValue = options?.PaidTier, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.AccountCancelDate))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().AccountCancelDate, new MessageAttributeValue() { StringValue = options?.AccountCancelDate, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.ContributionStatus))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().ContributionStatus, new MessageAttributeValue() { StringValue = options?.ContributionStatus, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.Revenue))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().Revenue, new MessageAttributeValue() { StringValue = options?.Revenue, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.HasAchieved2MonthsOfRevenue))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().HasAchieved2MonthsOfRevenue, new MessageAttributeValue() { StringValue = options?.HasAchieved2MonthsOfRevenue, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.HasAchieved3ConsecutiveMonthsOfRevenue))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().HasAchieved3ConsecutiveMonthsOfRevenue, new MessageAttributeValue() { StringValue = options?.HasAchieved3ConsecutiveMonthsOfRevenue, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(createdCohereAccount))
                {
                    messageAttributes.Add("_cca", new MessageAttributeValue() { StringValue = createdCohereAccount, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(cohereAccountType))
                {
                    messageAttributes.Add("_cat", new MessageAttributeValue() { StringValue = cohereAccountType, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(contactEmail))
                {
                    messageAttributes.Add("_ce", new MessageAttributeValue() { StringValue = contactEmail, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.LastCohereActivity))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().LastCohereActivity, new MessageAttributeValue() { StringValue = options?.LastCohereActivity, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(accountName))
                {
                    messageAttributes.Add("_an", new MessageAttributeValue() { StringValue = accountName, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.InvitedBy))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().InvitedBy, new MessageAttributeValue() { StringValue = options?.InvitedBy, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.FirstContributionCreationDate))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().FirstContributionCreationDate, new MessageAttributeValue() { StringValue = options?.FirstContributionCreationDate, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.PaidTierCreditCardStatus))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().PaidTierCreditCardStatus, new MessageAttributeValue() { StringValue = options?.PaidTierCreditCardStatus, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.NumberOfReferrals))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().NumberOfReferrals, new MessageAttributeValue() { StringValue = options?.NumberOfReferrals, DataType = "String" });
                }
                if (!string.IsNullOrWhiteSpace(options?.AffiliateRevenueEarned))
                {
                    messageAttributes.Add(new ActiveCampaignDealMessageKey().AffiliateRevenueEarned, new MessageAttributeValue() { StringValue = options?.AffiliateRevenueEarned, DataType = "String" });
                }

                var request = new SendMessageRequest()
                {
                    MessageBody = JsonConvert.SerializeObject(evnt),
                    QueueUrl = _activeCampaignQueueUrl,
                    MessageAttributes = messageAttributes
                };

                return _amazonSqs.SendMessageAsync(request);
            }
            catch
			{
                return null;
			}
        }
	}
}
