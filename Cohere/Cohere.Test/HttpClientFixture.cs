using System;
using System.IO;
using System.Net.Http;
using Cohere.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Cohere.Test
{
    /// <summary>
    /// Initialize Http client for testing for all test classes
    /// </summary>
    public class HttpClientFixture : IDisposable
    {
        public HttpClient Client { get; private set; }

        public HttpClientFixture()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            // overwrite if azure db test
            if (BaseTest.RemoteService)
            {
                configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings-remote.json").Build();
            }

            IWebHostBuilder whb = new WebHostBuilder().UseStartup<Startup>();
            whb.UseConfiguration(configuration);
            var server = new TestServer(whb);
            Client = server.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}