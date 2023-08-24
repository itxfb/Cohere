namespace Cohere.Api.Settings
{
    public class JwtSettings
    {
        public string Issuer { get; set; }

        public string Audience { get; set; }

        public string KeyId { get; set; }

        public int LifetimeSeconds { get; set; }
    }
}
