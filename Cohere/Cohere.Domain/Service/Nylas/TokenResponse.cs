namespace Cohere.Domain.Service.Nylas
{
    public class TokenResponse
    {
        public string access_token { get; set; }

        public string account_id { get; set; }

        public string email_address { get; set; }

        public string provider { get; set; }

        public string token_type { get; set; }
    }
}