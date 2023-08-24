using Cohere.Entity.Entities.Contrib;

namespace Cohere.Domain.Utils
{
    public static class LiveVideoProviderHelper
    {
        public static string GetLocationUrl(this LiveVideoProvider liveVideoProvider, string contributionviewUrl)
        {
            return liveVideoProvider.ProviderName.ToUpper() == Constants.LiveVideoProviders.Custom.ToUpper()
                ? liveVideoProvider.CustomLink ?? contributionviewUrl
                : contributionviewUrl;
        }
    }
}