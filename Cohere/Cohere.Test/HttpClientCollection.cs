using Xunit;

namespace Cohere.Test
{
    [CollectionDefinition("HttpClient collection")]
    public class HttpClientCollection : ICollectionFixture<HttpClientFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}