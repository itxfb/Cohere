/// <summary>
/// Designed by AnaSoft Inc. 2019
/// http://www.anasoft.net/apincore
///
/// NOTE: Tests are not working with InMemory database.
///       Must update database connection in appsettings.json - "CohereDB".
///       Initial database and tables will be created and seeded once during tests startup
///
///
/// AUTHENTICATION:
/// This test drives which authentication/authorization mechanism is used.
/// Update appsettings.json to switch between
/// "UseIndentityServer4": false = uses embeded JWT authentication
/// "UseIndentityServer4": true  =  uses IndentityServer 4
/// IMPORTANT: Before run IS4 test must build the solution and run once solution with IndentityServer as startup project
///            After you get the start page for IndentityServer4 you can stop run and run unit tests
/// </summary>

namespace Cohere.Test
{
    public class BaseTest
    {
        public static bool RemoteService { get; } = false;

        public static string UserName { get; } = "my@email.com";

        public static string Password { get; } = "mysecretpassword123";
    }

    // https://xunit.net/docs/shared-context#collection-fixture
}
