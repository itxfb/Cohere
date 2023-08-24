using Cohere.Domain.Models;
using Cohere.Domain.Models.Account;
using Cohere.Entity.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Test
{
    public class Utility
    {
        public async Task<int> AddAccount(HttpClient client)
        {
            AccountViewModel account = new AccountViewModel
            {
                Email = "apincore@anasoft.net",
                IsAccountLocked = false,
                IsEmailNotificationsEnabled = true,
                CreateTime = DateTime.Now
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/accountasync", new StringContent(
                JsonConvert.SerializeObject(account), Encoding.UTF8, "application/json"));
            var jsonString = await response.Content.ReadAsStringAsync();
            int lastAdded = 0;
            int.TryParse(jsonString, out lastAdded);
            return lastAdded;
        }

        public async Task<AccountViewModel> GetAccount(HttpClient client, int id)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            return account;
        }

        public async Task RemoveAccount(HttpClient client, int id)
        {
            await client.DeleteAsync("/api/account/" + id.ToString());
        }

        public async Task<int> AddUser(HttpClient client, int accountId)
        {
            UserViewModel user = new UserViewModel
            {
                FirstName = "User 1",
                LastName = "LastName",
                Roles = new List<Roles>(),
                StreetAddress = "Some address",
                AccountId = "someId"
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/userasync", new StringContent(
                JsonConvert.SerializeObject(user), Encoding.UTF8, "application/json"));
            var jsonString = await response.Content.ReadAsStringAsync();
            int lastAdded = 0;
            int.TryParse(jsonString, out lastAdded);
            return lastAdded;
        }

        public async Task<UserViewModel> GetUser(HttpClient client, int id)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            return user;
        }

        public async Task RemoveUser(HttpClient client, int id)
        {
            await client.DeleteAsync("/api/user/" + id.ToString());
        }
    }
}