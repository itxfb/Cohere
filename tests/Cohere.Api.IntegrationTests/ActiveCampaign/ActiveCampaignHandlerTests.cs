//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Amazon.SQS;
//using Amazon.SQS.Model;
//using Cohere.Domain.Models.Account;
//using Cohere.Domain.Service;
//using Cohere.Domain.Service.Abstractions.Generic;
//using Cohere.Domain.Service.Workers;
//using Cohere.Domain.Utils;
//using Cohere.Entity.Entities.ActiveCampaign;
//using Cohere.Entity.Infrastructure.Options;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using Newtonsoft.Json;
//using RestSharp;
//using Account = Cohere.Entity.Entities.Account;

//namespace Cohere.Api.IntegrationTests.ActiveCampaign
//{
//    [TestClass]
//    public class ActiveCampaignHandlerTests
//    {
//        private readonly ActiveCampaignHandler _activeCampaignHandler;
//        private readonly IActiveCampaignService _activeCampaignService;

//        public ActiveCampaignHandlerTests()
//        {
//            var amazonSqsMock = new Mock<IAmazonSQS>();
//            var loggerMock = new Mock<ILogger<ActiveCampaignHandler>>();
//            var activeCampaignOptions = Options.Create(new ActiveCampaignSettings()
//            {
//                ApiToken = "d4c097d6970d8c9aaf2a0f2e412af7f7cd3e7bfae1dba01c624807fd8e93d835320b0cc5",
//                BaseUrl = "https://cohereinc1621357793.api-us1.com/api/3"
//            });
//            var sqsOptions = Options.Create(new SqsSettings()
//            {
//                ActiveCampaignQueueUrl = "",
//            });
//            var activeCampaignClient = new ActiveCampaignClient(activeCampaignOptions, new RestClient());
//            _activeCampaignService = new ActiveCampaignService(activeCampaignClient, amazonSqsMock.Object, sqsOptions);
//            _activeCampaignHandler = new ActiveCampaignHandler(loggerMock.Object, amazonSqsMock.Object, "", _activeCampaignService);
//        }

//        [TestMethod]
//        public async Task ExecuteAsync_ActiveCampaignAccount_ShouldXXX()
//        {
//            //Arrange
//            var name = Guid.NewGuid().ToString();
//            var activeCampaignAccount = new ActiveCampaignAccount()
//            {
//                AccountUrl = "",
//                Name = name,
//                Fields = new List<ActiveCampaignCustomFields>()
//                {
//                    new ActiveCampaignCustomFields()
//                    {
//                        CustomFieldId = 12,
//                        FieldValue = "Coach Account Activated"
//                    }
//                }
//            };

//            var message = new Message()
//            {
//                Body = JsonConvert.SerializeObject(activeCampaignAccount),
//                MessageAttributes = new Dictionary<string, MessageAttributeValue>()
//                {
//                    { "_t", new MessageAttributeValue(){ DataType = "String", StringValue = nameof(ActiveCampaignAccount)}}
//                }
//            };

//            //Act
//            var result = await _activeCampaignHandler.HandleEventAsync(message) as ActiveCampaignAccountResponse;

//            //Assert
//            Assert.IsNotNull(result.Account.Id);
//            Assert.AreEqual(activeCampaignAccount.Name, result.Account.Name);
//            Assert.AreEqual(activeCampaignAccount.Fields[0].FieldValue, result.Account.Fields[0].FieldValue);
//        }

//        [TestMethod]
//        public async Task ExecuteAsync_ActiveCampaignContact_ShouldXXX()
//        {
//            //Arrange
//            var email = $"test{Guid.NewGuid()}@gmail.com";
//            var activeCampaignContact = new ActiveCampaignContact()
//            {
//                Email = email,
//                //FirstName = "test",
//                //LastName = "test",
//                FieldValues = new List<ActiveCampaignContactFields>()
//                {
//                    new ActiveCampaignContactFields()
//                    {
//                        Field = "1",
//                        Value = "Coach Account Activated"
//                    }
//                }
//            };

//            var message = new Message()
//            {
//                Body = JsonConvert.SerializeObject(activeCampaignContact),
//                MessageAttributes = new Dictionary<string, MessageAttributeValue>()
//                {
//                    { "_t", new MessageAttributeValue(){ DataType = "String", StringValue = nameof(ActiveCampaignContact)}}
//                }
//            };

//            //Act
//            var result = await _activeCampaignHandler.HandleEventAsync(message) as ActiveCampaignContactResponse;
            
//            //Assert
//            Assert.IsNotNull(result.Contact.Id);
//            Assert.AreEqual(activeCampaignContact.Email, result.Contact.Email);
//            Assert.AreEqual(activeCampaignContact.FieldValues[0].Value, result.FieldValues[0].Value);
//        }

//        [TestMethod]
//        public async Task ExecuteAsync_ActiveCampaignDeal_ShouldXXX()
//        {
//            var contact = await _activeCampaignService.GetContactByEmailAsync("coheretestac@mailinator.com");

//            //Arrange
//            var activeCampaignDeal = new ActiveCampaignDeal()
//            {
//                Title = "Sales Request Acct: Name",
//				//Account = "18",
//				Contact = contact?.Id.ToString(),
//                Owner = "1",
//                Status = (int)DealStatus.Open,
//                Stage = ((int)DealStage.Cold).ToString(),
//                Currency = "USD",
//                Value = "29000"

//            };

//            var message = new Message()
//            {
//                Body = JsonConvert.SerializeObject(activeCampaignDeal),
//                MessageAttributes = new Dictionary<string, MessageAttributeValue>()
//                {
//                    { "_t", new MessageAttributeValue(){ DataType = "String", StringValue = nameof(ActiveCampaignDeal)}}
//                }
//            };

//            //Act
//            var result = await _activeCampaignHandler.HandleEventAsync(message) as ActiveCampaignDealResponse;

//            //Assert
//            Assert.IsNotNull(result?.Deal?.Account);
//            Assert.AreEqual(activeCampaignDeal.Account, result?.Deal?.Account);
//            Assert.AreEqual(activeCampaignDeal.Fields[0].FieldValue, result?.Deal?.Fields[0].FieldValue);
//        }
//    }
//}
