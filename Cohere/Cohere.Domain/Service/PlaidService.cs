using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class PlaidService
    {
        public const string PlaidPublicKey = "PlaidPublicKey";
        public const string PlaidClientId = "PlaidClientId";
        public const string PlaidSecret = "PlaidSecret";
        public const string PlaidExchangePublicTokenUrl = "PlaidExchangePublicTokenUrl"; //$"{PlaidDomain}/item/public_token/exchange";
        public const string PlaidFetchStripeTokenUrl = "PlaidFetchStripeTokenUrl"; //$"{PlaidDomain}/processor/stripe/bank_account_token/create";

        private readonly string _plaidExchangePublicTokenUrl;
        private readonly string _plaidFetchStripeTokenUrl;
        private readonly string _plaidPublicKey;
        private readonly string _plaidClientId;
        private readonly string _plaidSecret;

        private JObject CredentialsBody => new JObject { { "client_id", _plaidClientId }, { "secret", _plaidSecret } };

        public PlaidService(Func<string, string> plaidUrlsResolver, Func<string, string> credentialsResolver)
        {
            _plaidExchangePublicTokenUrl = plaidUrlsResolver.Invoke(PlaidExchangePublicTokenUrl);
            _plaidFetchStripeTokenUrl = plaidUrlsResolver.Invoke(PlaidFetchStripeTokenUrl);

            _plaidPublicKey = credentialsResolver.Invoke(PlaidPublicKey);
            _plaidClientId = credentialsResolver.Invoke(PlaidClientId);
            _plaidSecret = credentialsResolver.Invoke(PlaidSecret);
        }

        public string GetPublicKey()
        {
            return _plaidPublicKey;
        }

        public async Task<string> ExchangePublicTokenAsync(string publicToken)
        {
            var body = CredentialsBody;
            body.Add("public_token", publicToken);

            var response = await ExecutePostAsync(_plaidExchangePublicTokenUrl, body.ToString());

            var jsonResponse = (JObject)JsonConvert.DeserializeObject(response.Content);
            return jsonResponse["access_token"].Value<string>();
        }

        public async Task<string> FetchStripeTokenAsync(string accessToken, string accountId)
        {
            var body = CredentialsBody;
            body.Add("access_token", accessToken);
            body.Add("account_id", accountId);

            var response = await ExecutePostAsync(_plaidFetchStripeTokenUrl, body.ToString());

            var jsonResponse = (JObject)JsonConvert.DeserializeObject(response.Content);
            return jsonResponse["stripe_bank_account_token"].Value<string>();
        }

        private static async Task<IRestResponse> ExecutePostAsync(string url, string data)
        {
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", data, ParameterType.RequestBody);

            var client = new RestClient(url) { Timeout = -1 };
            return await client.ExecuteAsync(request);
        }
    }
}
