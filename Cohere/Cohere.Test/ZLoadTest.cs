using System.Threading.Tasks;
using Xunit;

namespace Cohere.Test
{
    [Collection("HttpClient collection")]
    public class ZLoadTest : BaseTest
    {
        public HttpClientFixture Fixture { get; }

        public ZLoadTest(HttpClientFixture fixture)
        {
            Fixture = fixture;
            var client = fixture.Client;
        }

        /// <summary>
        /// Load test
        /// --local service: BaseTest.RemoteService = false
        /// --remote service: BaseTest.RemoteService = true
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Fact]
        public async Task LoadTest()
        {
            int loopmax = 10;
            var client = Fixture.Client;
            if (string.IsNullOrEmpty(TokenTest.TokenValue))
            {
                await TokenTest.TokenGet(client);
            }

            var accountId = 0;
            var userId = 0;
            var util = new Utility();
            int i = 1;
            while (i < loopmax)
            {
                accountId = await util.AddAccount(client);
                userId = await util.AddUser(client, accountId);
                await util.GetAccount(client, accountId);
                await util.GetUser(client, userId);
                await util.RemoveUser(client, userId);
                await util.RemoveAccount(client, accountId);
                i++;
            }

            Assert.True(i == loopmax);
        }
    }
}