using System;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.Extensions.Options;
using RestSharp;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Utils
{
    /// <summary>
    /// Low-level client for active campaign
    /// </summary>
    public interface IActiveCampaignClient
    {
        /// <summary>
        /// Get active campaign resource item
        /// </summary>
        /// <typeparam name="TR">Response type</typeparam>
        /// <param name="resource">Requested resource, Ex: accounts, deals, addresses</param>
        /// <param name="id">Resource id</param>
        /// <returns></returns>
        Task<TR> GetAsync<TR>(string resource, string id);

        Task<TR> GetAsync<TR>(string resource, string extraSubSegment1 = "", string extraSubSegment2 = "", string email = null, string status = null, string contact = null, int limit = 0);

        /// <summary>
        /// Create active campaign resource item
        /// </summary>
        /// <typeparam name="T">ActiveCampaign resource model</typeparam>
        /// <typeparam name="TR">Response type</typeparam>
        /// <param name="resource">Requested resource, Ex: accounts, deals, addresses</param>
        /// <param name="payload">Payload for resource item creation</param>
        /// <returns></returns>
        Task<TR> PostAsync<T, TR>(string resource, T payload);

        /// <summary>
        /// Update active campaign resource item
        /// </summary>
        /// <typeparam name="T">Resource id</typeparam>
        /// <typeparam name="TR">Response type</typeparam>
        /// <param name="id">Resource id</param>
        /// <param name="resource">Requested resource, Ex: accounts, deals, addresses</param>
        /// <param name="payload">Payload for resource item update</param>
        /// <returns></returns>
        Task<TR> PutAsync<T, TR>(string resource, string id, T payload);

        /// <summary>
        /// Delete active campaign resource item
        /// </summary>
        /// <typeparam name="TR">Response type</typeparam>
        /// <param name="resource">Requested resource, Ex: accounts, deals, addresses</param>
        /// <param name="id">Resource id</param>
        /// <returns></returns>
        Task<TR> DeleteAsync<TR>(string resource, string id);
    }
    public class ActiveCampaignClient : IActiveCampaignClient
    {
        private readonly string _apiToken;
        private readonly string _baseUrl;
        private readonly IRestClient _restClient;
        private readonly ILogger<ActiveCampaignClient> _logger;

        public ActiveCampaignClient(IOptions<ActiveCampaignSettings> activeCampaignOptions, IRestClient restClient,
            ILogger<ActiveCampaignClient> logger)
        {
            _restClient = restClient;
            _apiToken = activeCampaignOptions.Value.ApiToken;
            _baseUrl = activeCampaignOptions.Value.BaseUrl;
            _restClient.BaseUrl = new Uri(_baseUrl);
            _logger = logger;
        }

        public async Task<TR> GetAsync<TR>(string resource, string id)
		{
            return await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(2), new System.Threading.CancellationToken(), () => GetFunctionAsync<TR>(resource, id),
                _logger);
        }

        private async Task<TR> GetFunctionAsync<TR>(string resource, string id)
        {
            var request = new RestRequest("{resource}/{id}", Method.GET)
                .AddUrlSegment("resource", resource)
                .AddUrlSegment("id", id);
            request.AddHeader("Api-Token", _apiToken);

            var response = await _restClient.ExecuteAsync<TR>(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException(JsonSerializer.Serialize(response));
            }
            return response.Data;
        }

        public async Task<TR> GetAsync<TR>(string resource, string extraSubSegment1 = "", string extraSubSegment2 = "", string email = null, string status = null, string contact = null, int limit = 0)
		{
            return await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(2), new System.Threading.CancellationToken(), () => GetFunctionAsync<TR>(resource, extraSubSegment1, extraSubSegment2, email, status, contact,limit),
                _logger);
        }

        private async Task<TR> GetFunctionAsync<TR>(string resource, string extraSubSegment1 = "", string extraSubSegment2 = "", string email = null, string status = null, string contact = null, int limit = 0)
        {
            string resourceString = "{resource}";
            if(!string.IsNullOrEmpty(extraSubSegment1))
			{
                resourceString += "/{" + extraSubSegment1 + "}";
            }
            if (!string.IsNullOrEmpty(extraSubSegment2))
            {
                resourceString += "/{" + extraSubSegment2 + "}";
            }
            var request = new RestRequest(resourceString, Method.GET)
                .AddUrlSegment("resource", resource);
            if (!string.IsNullOrEmpty(extraSubSegment1))
            {
                request.AddUrlSegment(extraSubSegment1, extraSubSegment1);
            }
            if (!string.IsNullOrEmpty(extraSubSegment2))
            {
                request.AddUrlSegment(extraSubSegment2, extraSubSegment2);
            }
            request.AddParameter("limit", limit.ToString());
			if (!string.IsNullOrEmpty(email))
			{
                request.AddParameter("email", email);
			}
            if (!string.IsNullOrEmpty(contact))
            {
                request.AddParameter("contact", contact);
            }

            request.AddHeader("Api-Token", _apiToken);

            var response = await _restClient.ExecuteAsync<TR>(request);
            if(!response.IsSuccessful)
			{
                throw new HttpRequestException(JsonSerializer.Serialize(response));
            }
            return response.Data;
        }

        public async Task<TR> PostAsync<T, TR>(string resource, T payload)
		{
            return await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(2), new System.Threading.CancellationToken(), () => PostFunctionAsync<T, TR>(resource, payload),
                _logger);
		}

        private async Task<TR> PostFunctionAsync<T, TR>(string resource, T payload)
        {
            var request = new RestRequest("{resource}", Method.POST)
                .AddUrlSegment("resource", resource);
            request.AddHeader("Api-Token", _apiToken);

            var jsonBody = JsonSerializer.Serialize(payload);
            request.AddJsonBody(jsonBody);
            var response = await _restClient.ExecuteAsync<TR>(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException(JsonSerializer.Serialize(response));
            }
            return response.Data;
        }

        public async Task<TR> PutAsync<T, TR>(string resource, string id, T payload)
		{
            return await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(2), new System.Threading.CancellationToken(), () => PutFunctionAsync<T, TR>(resource, id, payload),
                _logger);
        }

        private async Task<TR> PutFunctionAsync<T, TR>(string resource, string id, T payload)
        {
            var request = new RestRequest("{resource}/{id}", Method.PUT)
                .AddUrlSegment("resource", resource)
                .AddUrlSegment("id", id);
            request.AddHeader("Api-Token", _apiToken);

            var jsonBody = JsonSerializer.Serialize(payload);
            request.AddJsonBody(jsonBody);
            var response = await _restClient.ExecuteAsync<TR>(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException(JsonSerializer.Serialize(response));
            }
            return response.Data;
        }

		public async Task<TR> DeleteAsync<TR>(string resource, string id)
		{
            return await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(2), new System.Threading.CancellationToken(), () => DeleteFunctionAsync<TR>(resource, id),
                _logger);
        }

        private async Task<TR> DeleteFunctionAsync<TR>(string resource, string id)
        {
            var request = new RestRequest("{resource}/{id}", Method.DELETE)
                .AddUrlSegment("resource", resource)
                .AddUrlSegment("id", id);
            request.AddHeader("Api-Token", _apiToken);
            
            var response = await _restClient.ExecuteAsync<TR>(request);
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException(JsonSerializer.Serialize(response));
            }
            return response.Data;
        }
    }
}
