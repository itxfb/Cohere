namespace Cohere.Entity.Infrastructure.Options
{
    public class SecretsSettings
    {
        public string PasswordEncryptionKey { get; set; }

        public string JwtRsaPrivateKeyXml { get; set; }

        public string JwtRsaPublicKeyXml { get; set; }

        public string TwilioAccountAuthToken { get; set; }

        public string TwilioApiSecret { get; set; }

        public string NylasClientId { get; set; }

        public string NylasClientSecret { get; set; }
        
        public string MasterPassword { get; set; }
    }
}
