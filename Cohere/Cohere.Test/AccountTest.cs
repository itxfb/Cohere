using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Cohere.Domain.Models.Account;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Cohere.Test
{
    /// <summary>
    /// Account API Integration tests
    /// </summary>
    [Collection("HttpClient collection")]
    public class AccountTest : BaseTest
    {
        public HttpClientFixture Fixture { get; }

        public AccountTest(HttpClientFixture fixture)
        {
            Fixture = fixture;
            var client = fixture.Client;
        }

        [Fact]
        public async Task Account_getall()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/account");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);

            //clean
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task Account_add_update_delete()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            //insert
            AccountViewModel vmentity = new AccountViewModel
            {
                Email = "apincore@anasoft.net",
                OnboardingStatus = "desc",
                IsAccountLocked = false,
                IsEmailNotificationsEnabled = true,
                CreateTime = DateTime.Now
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/account", new StringContent(
                JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0;
            int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetAccount(client, id);

            //update test
            vmentity.OnboardingStatus = "desc updated";
            response = await client.PutAsync("/api/account/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/account/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.OnboardingStatus);

            //another update with same account - concurrency
            vmentity.OnboardingStatus = "desc updated 2";
            response = await client.PutAsync("/api/account/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test
            response = await client.DeleteAsync("/api/account/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Account_getbyid()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/account/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Email == "Account");

            //clean
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task Account_getactivebyname()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);

            //get by id
            var response = await client.GetAsync("/api/account/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);

            response = await client.GetAsync("/api/account/GetActiveByName/" + account.Email);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);

            //clean
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task Account_getallasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);

            //clean
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task Account_add_update_delete_async()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            //insert
            AccountViewModel vmentity = new AccountViewModel
            {
                Email = "apincore@anasoft.net",
                OnboardingStatus = "desc",
                IsAccountLocked = false,
                IsEmailNotificationsEnabled = true,
                CreateTime = DateTime.Now
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/accountasync", new StringContent(
                JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0;
            int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetAccount(client, id);

            //update test
            vmentity.OnboardingStatus = "desc updated";
            response = await client.PutAsync("/api/accountasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.OnboardingStatus);

            //another update with same account - concurrency
            vmentity.OnboardingStatus = "desc updated 2";
            response = await client.PutAsync("/api/accountasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test
            response = await client.DeleteAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Account_getbyidasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Email == "Account");

            //clean
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task Account_getactivebynameasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);

            //get by id
            var response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);

            response = await client.GetAsync("/api/accountasync/GetActiveByName/" + account.Email);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);

            //clean
            await util.RemoveAccount(client, accountid);
        }
    }
}