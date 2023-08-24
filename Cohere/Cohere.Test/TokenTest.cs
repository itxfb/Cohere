using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Cohere.Api.Controllers;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Xunit;

namespace Cohere.Test
{
    [Collection("HttpClient collection")]
    public class TokenTest : BaseTest
    {
        static HttpClient _client;

        public HttpClientFixture Fixture { get; }

        public TokenTest(HttpClientFixture fixture)
        {
            Fixture = fixture;
            _client = fixture.Client;
        }

        public static string TokenValue { get; set; }

        public static async Task TokenGet(HttpClient client)
        {
            if (client == null)
            {
                client = _client;
            }

            if (!string.IsNullOrEmpty(TokenValue))
            {
                return;
            }

            //read from tests settings
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            //JWT or IS4 authentication
            if (configuration["Authentication:UseIndentityServer4"] == "False")
            { //JWT
                AuthController.LoginModel login = new AuthController.LoginModel { Username = UserName, Password = Password };
                var response = await client.PostAsync("/api/token", new StringContent(JsonConvert.SerializeObject(login), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var jsonString = await response.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<Token>(jsonString);
                TokenValue = token.TokenValue;
            }
            else
            { //IS4
                var is4ip = configuration["Authentication:IndentityServer4IP"];

                // discover endpoints from the metadata by calling Auth server hosted on 5000 port
                var discoveryClient = await DiscoveryClient.GetAsync(is4ip);
                if (discoveryClient.IsError)
                {
                    Console.WriteLine(discoveryClient.Error);
                    throw new Exception(discoveryClient.Error);
                }
                //// request the token from the Auth server for type ClientCredentials
                //var tokenClient1 = new TokenClient(discoveryClient.TokenEndpoint, "clientCred", "secret");
                //var response1 = await tokenClient1.RequestClientCredentialsAsync("Cohere");
                //var resp1 = response1.Json;

                //BAD client test
                var tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "CohereClient-BAD", "secret");
                var response = await tokenClient.RequestResourceOwnerPasswordAsync("my@email.com", "mysecretpassword123", "Cohere");
                var response_json = response.Json;
                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }

                Assert.True(response.IsError);
                Assert.Equal("invalid_client", response.Error);
                Assert.Equal(HttpStatusCode.BadRequest, response.HttpStatusCode);

                //BAD grant test
                tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "CohereClient", "secret");
                response = await tokenClient.RequestResourceOwnerPasswordAsync("my@email.com", "mysecretpassword123-BAD", "Cohere");
                response_json = response.Json;

                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }

                Assert.True(response.IsError);
                Assert.Equal("invalid_grant", response.Error);
                Assert.Equal(HttpStatusCode.BadRequest, response.HttpStatusCode);

                //GOOD TEST----------------
                //use your own user list (from database) to get a token for API user
                tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "CohereClient", "secret");
                response = await tokenClient.RequestResourceOwnerPasswordAsync(UserName, Password, "Cohere");
                response_json = response.Json;

                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }

                Assert.False(response.IsError);
                Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
                var jsonString = response.AccessToken;
                var token = new Token
                {
                    TokenValue = jsonString
                };
                TokenValue = token.TokenValue;
            }
        }

        /// <summary>
        /// This test drives which authentication/authorization mechanism is used.
        /// Update appsettings.json to switch between
        /// "UseIndentityServer4": false = uses embeded JWT authentication
        /// "UseIndentityServer4": true  =  uses IndentityServer 4
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Fact]
        public async Task Token_test()
        {
            await TokenGet(null);
        }
    }
}