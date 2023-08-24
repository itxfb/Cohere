namespace Cohere.Domain.Models.Account
{
    public class TokenVerificationViewModel
    {
        private string _emailLower;

        public string Email
        {
            get => _emailLower?.ToLower();
            set => _emailLower = value;
        }

        public string Token { get; set; }
    }
}
