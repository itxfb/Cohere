using Cohere.Domain.Models;
using Cohere.Entity.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cohere.Test
{
    [Collection("HttpClient collection")]
    public class UserTest : BaseTest
    {
        public HttpClientFixture Fixture { get; }

        public UserTest(HttpClientFixture fixture)
        {
            Fixture = fixture;
            var client = fixture.Client;
        }

        public static string LastAddedUser { get; set; }

        [Fact]
        public async Task User_getall()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/user");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task User_add_update_delete()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            //insert
            UserViewModel vmentity = new UserViewModel
            {
                FirstName = "User 1",
                LastName = "LastName",
                Roles = new List<Roles>(),
                StreetAddress = "Some address",
                AccountId = "someId"
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/user", new StringContent(
                JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0;
            int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetUser(client, id);

            //update test
            vmentity.LastName = "desc updated";
            response = await client.PutAsync("/api/user/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/user/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.LastName);

            //another update with same account - concurrency
            vmentity.LastName = "desc updated 2";
            response = await client.PutAsync("/api/user/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test
            response = await client.DeleteAsync("/api/user/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task User_getbyid()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.FirstName == "FirstName");

            // lazy-loading test
            //response = await client.GetAsync("/api/account/" + accountid.ToString());
            //response.EnsureSuccessStatusCode();
            //Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            //jsonString = await response.Content.ReadAsStringAsync();
            //var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            //Assert.True(account.Users.Count == 1);

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task User_getactivebyfirstname()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);

            //get by id
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);

            response = await client.GetAsync("/api/user/GetActiveByFirstName/" + user.FirstName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task User_getallasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);

            // lazy-loading test
            //response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            //response.EnsureSuccessStatusCode();
            //Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            //jsonString = await response.Content.ReadAsStringAsync();
            //var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            //Assert.True(account.Users.Count == 1);

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task User_add_update_delete_async()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            //insert
            UserViewModel vmentity = new UserViewModel
            {
                FirstName = "User 1",
                LastName = "LastName",
                Roles = new List<Roles>(),
                StreetAddress = "Some address",
                AccountId = "someId"
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/userasync", new StringContent(
                JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id;
            int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetUser(client, id);

            //update test
            vmentity.LastName = "desc updated";
            response = await client.PutAsync("/api/userasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.LastName);

            //another update with same account - concurrency
            vmentity.LastName = "desc updated 2";
            response = await client.PutAsync("/api/userasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test
            response = await client.DeleteAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task User_getbyidasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.FirstName == "FirstName");

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }

        [Fact]
        public async Task User_getactivebyfirstnameasync()
        {
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var util = new Utility();
            var accountid = await util.AddAccount(client);
            var userid = await util.AddUser(client, accountid);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);

            //get by id
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);

            response = await client.GetAsync("/api/userasync/GetActiveByFirstName/" + user.FirstName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);

            //clean
            await util.RemoveUser(client, userid);
            await util.RemoveAccount(client, accountid);
        }
    }
}