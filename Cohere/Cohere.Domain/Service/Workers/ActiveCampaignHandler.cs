using Amazon.SQS;
using Amazon.SQS.Model;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Cohere.Domain.Utils.Constants.TemplatesPaths;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service.Workers
{
    public class ActiveCampaignHandler : BackgroundService
    {
        private readonly IAmazonSQS _amazonSqs;
        private readonly ILogger<ActiveCampaignHandler> _logger;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly string _activeCampaignQueueUrl;
        private readonly IServiceAsync<AccountViewModel, Account> _accountService;

        public ActiveCampaignHandler(ILogger<ActiveCampaignHandler> logger, 
            IAmazonSQS amazonSqs,
            string activeCampaignQueueUrl,
            IActiveCampaignService activeCampaignService,
            IServiceAsync<AccountViewModel, Account> accountService)
        {
            _logger = logger;
            _amazonSqs = amazonSqs;
            _activeCampaignQueueUrl = activeCampaignQueueUrl;
            _activeCampaignService = activeCampaignService;
            _accountService = accountService;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new ReceiveMessageRequest
                    {
                        QueueUrl = _activeCampaignQueueUrl,
                        WaitTimeSeconds = 5,
                        MessageAttributeNames = new List<string> { "_.*" },
                    };

                    var messages = (await _amazonSqs.ReceiveMessageAsync(request, cancellationToken)).Messages;
                    foreach (var message in messages)
                    {
                        try
                        {
                            _logger.LogInformation("AC - Start processing message {message} | {time}", message.Body, DateTime.UtcNow);

                            try
                            {
                                await HandleEventAsync(message);
                            }
                            catch(Exception ex)
							{
                                _logger.LogError(ex, "Error during handle active campaign HandleEventAsync");
                            }
                            
                            await _amazonSqs.DeleteMessageAsync(_activeCampaignQueueUrl, message.ReceiptHandle);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error during handle active campaign event");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during handle active campaign event");
                }
            }
        }

        public async Task<object> HandleEventAsync(Message message)
        {
            _logger.LogInformation("AC - HandleEventAsync- Start");
            if (!message.MessageAttributes.TryGetValue("_type", out var eventType))
            {
                throw new ArgumentNullException(nameof(eventType));
            }

            _logger.LogInformation($"AC - HandleEventAsync- event type - {eventType.StringValue}");
            if (eventType.StringValue == nameof(ActiveCampaignContact))
            {
                var contact = JsonConvert.DeserializeObject<ActiveCampaignContact>(message.Body);
                var existingContact = await _activeCampaignService.GetContactByEmailAsync(contact.Email);
                if (existingContact != null)
                {
                    contact = existingContact;

                    // check if deal reord needs to be updated
                    var existingDeal = await _activeCampaignService.GetLatestDealAsync(contact.Email);
                    if (existingDeal != null)
                    {
                        var paidTierDealCustomFieldMeta = await _activeCampaignService.GetActiveCampaignDealCustomFieldMetaByLabel(new DealCustomFieldLabel().PaidTier);
                        if (paidTierDealCustomFieldMeta != null)
                        {
                            var existingField = await _activeCampaignService.GetActiveCampaignDealCustomFieldData(existingDeal.Id, paidTierDealCustomFieldMeta.Id.ToString());
                            if (existingField == null)
                            {
                                _activeCampaignService.CreateDealCustomFieldDataAsync(new ActiveCampaignDealCustomFieldData()
                                { DealId = existingDeal.Id, CustomFieldId = paidTierDealCustomFieldMeta.Id.ToString(), FieldValue = new CohereDealCustomFieldPaidTear().Launch }).GetAwaiter().GetResult();
                            }
                        }
                    }
                }
                else
                {
                    var createdCustomer = await _activeCampaignService.CreateContactAsync(contact);
                    _logger.LogInformation($"AC - Customer {createdCustomer?.Contact?.Email} Created");
                }

                var account = new ActiveCampaignAccount();
                if (message.MessageAttributes.TryGetValue("_an", out var accountName))
                {
                    account.Name = accountName.StringValue + " " + Guid.NewGuid().ToString();
                }
                ActiveCampaignContact contact2 = null;
                bool updated = false;
                if (message.MessageAttributes.TryGetValue("_ce", out var contactEmail))
                {
                    var existingContact2 = await _activeCampaignService.GetContactByEmailAsync(contactEmail.StringValue);
                    if (existingContact2 != null)
                    {
                        contact2 = existingContact2;
                        var existingAssociation = await _activeCampaignService.GetActiveCampaignAccountContactAssociationByContactId(existingContact2.Id);
                        if (existingAssociation != null)
                        {
                            var existingAccount = await _activeCampaignService.GetAccountAsync(existingAssociation.Account);
                            if (existingAccount != null)
                            {
                                updated = true;
                                account = existingAccount.Account;
                            }

                        }
                    }
                    else
					{
                        _logger.LogInformation($"AC - Client {contactEmail.StringValue} not found, can't create an account");
					}
                }
                
                if (message.MessageAttributes.TryGetValue("_cca", out var createdCohereAccount))
                {
                    var createdCohereAccountAccountCustomFieldMeta = await _activeCampaignService.GetActiveCampaignAccountCustomFieldMetaByLabel(new AccountCustomFieldLabel().CreatedCohereAccount);
                    if (createdCohereAccountAccountCustomFieldMeta != null)
                    {
                        if (updated)
                        {
                            var existingField = await _activeCampaignService.GetActiveCampaignAccountCustomFieldData(account.Id, createdCohereAccountAccountCustomFieldMeta.Id.ToString());
                            if (existingField != null)
                            {
                                //do nothing
                                //_activeCampaignService.UpdateAccountCustomFieldDataAsync(existingField.Id, new ActiveCampaignAccountCustomFieldDataUpdate() { FieldValue = createdCohereAccount.StringValue }).GetAwaiter().GetResult();
                            }
                            else
                            {
                                var createdCohereAcountResult = _activeCampaignService.CreateAccountCustomFieldDataAsync(new ActiveCampaignAccountCustomFieldData()
                                { AccountId = account.Id, CustomFieldId = createdCohereAccountAccountCustomFieldMeta.Id.ToString(), FieldValue = createdCohereAccount.StringValue }).GetAwaiter().GetResult();
                            }
                        }
                        else
                        {
                            account.Fields.Add(new ActiveCampaignCustomFields()
                            {
                                CustomFieldId = createdCohereAccountAccountCustomFieldMeta.Id,
                                FieldValue = createdCohereAccount.StringValue
                            });
                        }
                    }
                    //if (message.MessageAttributes.TryGetValue("_cat", out var cohereAccountType))
                    //{
                    //    var CohereAccountTypeAccountCustomFieldMeta = await _activeCampaignService.GetActiveCampaignAccountCustomFieldMetaByLabel(new AccountCustomFieldLabel().CohereAccountType);
                    //    if (CohereAccountTypeAccountCustomFieldMeta != null)
                    //    {
                    //        account.Fields.Add(new ActiveCampaignCustomFields()
                    //        {
                    //            CustomFieldId = CohereAccountTypeAccountCustomFieldMeta.Id,
                    //            FieldValue = cohereAccountType.StringValue
                    //        });
                    //    }
                    //}
                }
                if (updated)
                {
                    return await _activeCampaignService.UpdateAccountAsync(account.Id, account);
                }
                else
                {
                    _logger.LogInformation($"AC - Createing Active Campaign Account, Name: {account?.Name}, ID: {account?.Id}");
                    var result = await _activeCampaignService.CreateAccountAsync(account);
                    _logger.LogInformation($"AC - Account created = {(!string.IsNullOrEmpty(result?.Account?.Id)).ToString()}");
                    _logger.LogInformation($"AC - Checing if can create customer - account association. AccountID: ${result?.Account?.Id} ContactID: {contact2?.Id}");
                    if (!string.IsNullOrEmpty(result?.Account?.Id) && !string.IsNullOrEmpty(contact2?.Id))
                    {
                        _logger.LogInformation("AC - Createing Active Campaign Contact Account Association");
						var associationResult = await _activeCampaignService.CreateAccountContactAssociationAsync(new ActiveCampaignAccountContact() { Account = result.Account.Id, Contact = contact2.Id });
                        _logger.LogInformation($"AC - Association created = {(!string.IsNullOrEmpty(associationResult?.AccountContact?.Id)).ToString()}");
                    }
                    return result;
                }
            }
            else if (eventType.StringValue == nameof(ActiveCampaignDeal))
            {
                ActiveCampaignContact contact = null;
                string dealTitle = "";
                string stage = null;
                string group = null;
                if (message.MessageAttributes.TryGetValue("_cai", out var cohereAccountId))
                {
                    var account = await _accountService.GetOne(cohereAccountId.StringValue);
                    if (account != null)
                    {
                        dealTitle = account.Email;
                        contact = await _activeCampaignService.GetContactByEmailAsync(account.Email);
                        // if contact was not created yet, try to wait some time to fetch it
                        if(contact == null)
                        {
                            int retriesCount = 0;
                            while (contact == null && retriesCount <= 6)
                            {
                                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                                contact = await _activeCampaignService.GetContactByEmailAsync(account.Email);
                                retriesCount++;
                            }
                        }
                    }
                    if (message.MessageAttributes.TryGetValue("_s", out var stageName))
                    {
                        if (message.MessageAttributes.TryGetValue("_p", out var pipelineName))
                        {
                            var dealStage = await _activeCampaignService.GetStageByNameAndPipelineName(stageName.StringValue, pipelineName.StringValue);
                            if (dealStage != null)
                            {
                                stage = dealStage.Id;
                                group = dealStage.Group;
                            }
						}
                    }
                }
                
                if (contact != null)
                {
                    bool update = false;
					var deal = JsonConvert.DeserializeObject<ActiveCampaignDeal>(message.Body);
                    var dealCustomFields = new List<ActiveCampaignCustomFields>();
                    var existingDeal = await _activeCampaignService.GetLatestDealAsync(contact.Email);
                    // if deal was not created yet, try to wait some time to fetch it
                    if (existingDeal == null)
                    {
                        int retriesCount = 0;
                        while (existingDeal == null && retriesCount <= 6)
                        {
                            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                            existingDeal = await _activeCampaignService.GetLatestDealAsync(contact.Email);
                            retriesCount++;
                        }
                    }
                    if (existingDeal != null)
					{
                        update = true;
                        deal = existingDeal;
                    }

                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().PaidTier, new DealCustomFieldLabel().PaidTier, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().AccountCancelDate, new DealCustomFieldLabel().AccountCancelDate, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().ContributionStatus, new DealCustomFieldLabel().ContributionStatus, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().Revenue, new DealCustomFieldLabel().Revenue, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().HasAchieved2MonthsOfRevenue, new DealCustomFieldLabel().HasAchieved2MonthsOfRevenue, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().HasAchieved3ConsecutiveMonthsOfRevenue, new DealCustomFieldLabel().HasAchieved3ConsecutiveMonthsOfRevenue, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().LastCohereActivity, new DealCustomFieldLabel().LastCohereActivity, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().InvitedBy, new DealCustomFieldLabel().InvitedBy, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().FirstContributionCreationDate, new DealCustomFieldLabel().FirstContributionCreationDate, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().PaidTierCreditCardStatus, new DealCustomFieldLabel().PaidTierCreditCardStatus, update, deal);
                    await updateOrCreateDealFieldValue(message, new ActiveCampaignDealMessageKey().NumberOfReferrals, new DealCustomFieldLabel().NumberOfReferrals, update, deal);

                    if (!update)
                    {
                        deal.Contact = contact.Id.ToString();
                        deal.Title = dealTitle;
                        deal.Stage = stage;
                        deal.Group = group;
                        if (string.IsNullOrEmpty(deal.Owner))
                        {
                            deal.Owner = "1";
                        }
                        if (deal.Status == null)
                        {
                            deal.Status = (int)DealStatus.Open;
                        }
                        if (string.IsNullOrEmpty(deal.Currency))
                        {
                            deal.Currency = "USD";
                        }
                    }
                    if (update)
                    {
                        // do nothing
                        //return await _activeCampaignService.UpdateDealAsync(deal.Id, deal);
                    }
                    else
					{
                        // never create a deal for now
                        //return await _activeCampaignService.CreateDealAsync(deal);
                    }
                }
            }
            
            return null;
        }

        private async Task updateOrCreateDealFieldValue(Message message, string messageAttributeName, string dealCustomDieldLabel, bool update, ActiveCampaignDeal deal)
		{
            if (message.MessageAttributes.TryGetValue(messageAttributeName, out var messageValue))
            {
                var dealCustomFieldMeta = await _activeCampaignService.GetActiveCampaignDealCustomFieldMetaByLabel(dealCustomDieldLabel);
                if (dealCustomFieldMeta != null)
                {
                    if (update)
                    {
                        var existingField = await _activeCampaignService.GetActiveCampaignDealCustomFieldData(deal.Id, dealCustomFieldMeta.Id.ToString());
                        if (existingField != null)
                        {
                            _activeCampaignService.UpdateDealCustomFieldDataAsync(existingField.Id, new ActiveCampaignDealCustomFieldDataUpdate() { FieldValue = messageValue.StringValue }).GetAwaiter().GetResult();
                        }
                        else
                        {
                            _activeCampaignService.CreateDealCustomFieldDataAsync(new ActiveCampaignDealCustomFieldData()
                            { DealId = deal.Id, CustomFieldId = dealCustomFieldMeta.Id.ToString(), FieldValue = messageValue.StringValue }).GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        deal.Fields.Add(new ActiveCampaignCustomFields()
                        {
                            CustomFieldId = dealCustomFieldMeta.Id,
                            FieldValue = messageValue.StringValue
                        });
                    }
                }
            }
        }
    }
}
