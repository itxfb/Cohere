namespace Cohere.Domain.Models.Account
{
    public class LoginViewModel
    {
        private string _emailLower;

        public string Email
        {
            get => _emailLower?.ToLower();
            set => _emailLower = value;
        }

        public string Password { get; set; }
        public string DeviceToken { get; set; }
    }
}
