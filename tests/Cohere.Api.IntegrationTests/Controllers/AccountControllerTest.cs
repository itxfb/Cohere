using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.User;
using Cohere.Entity.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson.IO;

namespace Cohere.Api.IntegrationTests.Controllers
{
    [DoNotParallelize]
    [TestClass]
    public class AccountControllerTest : IntegrationTestBase
    {
        [TestMethod]
        public async Task CreateClient()
        {
            var createAccountResponseMessage = await Client.PostAsJsonAsync(ApiRoutes.Account.Create(), 
                new AccountViewModel()
                {
                    Email = $"va.leshkevich.andersenlab+{DateTime.UtcNow.Ticks}@gmail.com",
                    Password = "Testme1234"
                });

            var account = await createAccountResponseMessage.Content.ReadAsAsync<AccountViewModel>();

            Client.SetBearerToken(account.OAuthToken);
            
            var content = JsonSerializer.Serialize(new UserViewModel()
            {
                AccountId = account.Id,
                FirstName = "TestFirstName",
                LastName = "TestLastName",
                BirthDate = DateTime.Parse("1990-12-12T00:00:00Z"),
                HasAgreedToTerms = true,
                TimeZoneId = "America/Los_Angeles",
            });

            var stringContent = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);

            stringContent.Headers.ContentLength = content.Length;
            
            var createUserResponseMessage = await Client.PostAsync(ApiRoutes.User.Create(), stringContent);

            var user = await createUserResponseMessage.Content.ReadAsAsync<AccountAndUserAggregatedViewModel>();
            
            Assert.IsNotNull(user?.User?.Id);
            Assert.IsFalse(user.User.Id == string.Empty);
        }
        
        [TestMethod]
        public async Task CreateCoach()
        {
            var createAccountResponseMessage = await Client.PostAsJsonAsync(ApiRoutes.Account.Create(), 
                new AccountViewModel()
                {
                    Email = $"va.leshkevich.andersenlab+{DateTime.UtcNow.Ticks}@gmail.com",
                    Password = "Testme1234"
                });

            var account = await createAccountResponseMessage.Content.ReadAsAsync<AccountViewModel>();

            Client.SetBearerToken(account.OAuthToken);
            
            var content = JsonSerializer.Serialize(new UserViewModel()
            {
                AccountId = account.Id,
                FirstName = "TestFirstName",
                LastName = "TestLastName",
                BirthDate = DateTime.Parse("1990-12-12T00:00:00Z"),
                HasAgreedToTerms = true,
                TimeZoneId = "America/Los_Angeles",
                BusinessType = "Teaching",
                Occupation = "123",
                IsCohealer = true,
            });

            var stringContent = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);

            stringContent.Headers.ContentLength = content.Length;
            
            var createUserResponseMessage = await Client.PostAsync(ApiRoutes.User.Create(), stringContent);

            var user = await createUserResponseMessage.Content.ReadAsAsync<UserViewModel>();

            var response = await Client.GetAsync(ApiRoutes.Auth.GetAccountInfo());
            var model = await response.Content.ReadAsAsync<AccountAndUserWithRolesAggregateViewModel>();

            Assert.IsTrue(model.Roles.Contains(Roles.Cohealer.ToString()));
        }
    }
}