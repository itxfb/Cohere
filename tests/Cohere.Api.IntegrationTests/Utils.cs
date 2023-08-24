using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests
{
    public static class Utils
    {
        public static IConfiguration InitConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("tokens.json")
                .AddJsonFile("MongoSettings.json")
                .Build();
            return config;
        }

        public static T LoadJson<T>(string fileName)
        {
            using (StreamReader r = new StreamReader(fileName))
            {
                string json = r.ReadToEnd();
                T items = JsonConvert.DeserializeObject<T>(json);

                return items;
            }
        }

        public static async Task ThrowIfNotSuccess(HttpResponseMessage result)
        {
            if (!result.IsSuccessStatusCode)
            {
                throw new Exception(await result.Content.ReadAsStringAsync());
            }
        }
    }
}
