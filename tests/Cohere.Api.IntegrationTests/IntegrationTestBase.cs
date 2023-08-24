using Cohere.Api.IntegrationTests.Controllers;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mongo2Go;
using Stripe;
using System;
using System.Net.Http;

namespace Cohere.Api.IntegrationTests
{
    public class IntegrationTestBase
    {
        protected readonly HttpClient Client;

        public IServiceProvider AppServices { get; private set; }

        private readonly string _clientToken;
        private readonly string _coachToken;
        private readonly string _adminToken;

        public static readonly Lazy<WebApplicationFactory<Startup>> _factory = new Lazy<WebApplicationFactory<Startup>>(BuildTestApp, true);
        private static readonly Lazy<IConfiguration> _congif = new Lazy<IConfiguration>(() => Utils.InitConfiguration(), true);

        public static Lazy<MongoDbRunner> runner = new Lazy<MongoDbRunner>(() => {
            var runner = MongoDbRunner.StartForDebugging(port: 27777);
            SeedCollections(runner);
            return runner;
        });

        private static void SeedCollections(MongoDbRunner runner)
        {
            var dbSettings = _congif.Value.GetSection("MongoSettings").Get<MongoSettings>();
            var dbName = dbSettings.DatabaseName;

            runner.Import(dbName, "Accounts", "./JsonData/Seed/Accounts.json", false);
            runner.Import(dbName, "Users", "./JsonData/Seed/Users.json", false);
        }

        private static string ConnectionString => runner.Value.ConnectionString;

        public IntegrationTestBase()
        {
            Client = _factory.Value.CreateClient();
            AppServices = _factory.Value.Services;

            _coachToken = _congif.Value["CoachApiTestsToken"];
            _clientToken = _congif.Value["ClientApiTestsToken"];
            _adminToken = _congif.Value["AdminApiTestsToken"];
        }

        public void SetCoachAuthToken()
        {
            Client.SetBearerToken(_coachToken);
        }

        public void SetClientAuthToken()
        {
            Client.SetBearerToken(_clientToken);
        }

        public void SetAdminAuthToken()
        {
            Client.SetBearerToken(_adminToken);
        }

        public void ResetAuthToken()
        {
            Client.SetBearerToken(null);
        }

        private static WebApplicationFactory<Startup> BuildTestApp()
        {
            return new WebApplicationFactory<Startup>().WithWebHostBuilder(builder => {
                builder.ConfigureServices(cs => {
                    cs.Configure<MongoSecretsSettings>(c => {
                        c.MongoConnectionString = ConnectionString.EndsWith('/') ? ConnectionString.Remove(ConnectionString.Length - 1) : ConnectionString;
                    });
                    cs.AddTransient<PaymentMethodService>();
                    cs.AddTransient<IStripeActions, StripeActions>();
                });
            });
        }
    }

    [TestClass]
    public class Config
    {
        [AssemblyCleanup()]
        public static void Cleanup()
        {
            IntegrationTestBase._factory?.Value?.Dispose();
            IntegrationTestBase.runner?.Value?.Dispose();
        }
    }
}
