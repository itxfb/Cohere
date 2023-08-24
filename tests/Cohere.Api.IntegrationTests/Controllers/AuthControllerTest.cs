using Cohere.Domain.Models.Account;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests.Controllers
{
    [DoNotParallelize]
    [TestClass]
    public class AuthControllerTest : IntegrationTestBase
    {
        [TestMethod]
        public async Task ClientHasClientRole()
        {
            SetClientAuthToken();

            var response = await Client.GetAsync(ApiRoutes.Auth.GetAccountInfo());
            var model = await response.Content.ReadAsAsync<AccountAndUserWithRolesAggregateViewModel>();

            Assert.IsTrue(model.Roles.Contains("Client"));
        }

        [TestMethod]
        public async Task CoachHasCohealerRole()
        {
            SetCoachAuthToken();

            var response = await Client.GetAsync(ApiRoutes.Auth.GetAccountInfo());
            var model = await response.Content.ReadAsAsync<AccountAndUserWithRolesAggregateViewModel>();

            Assert.IsTrue(model.Roles.Contains("Cohealer"));
        }

        [TestMethod]
        public async Task AdminHasAdminRole()
        {
            SetAdminAuthToken();

            var response = await Client.GetAsync(ApiRoutes.Auth.GetAccountInfo());
            var model = await response.Content.ReadAsAsync<AccountAndUserWithRolesAggregateViewModel>();

            Assert.IsTrue(model.Roles.Contains("Admin"));
        }
    }
}
