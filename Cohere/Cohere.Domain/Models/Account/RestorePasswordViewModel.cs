namespace Cohere.Domain.Models.Account
{
    public class RestorePasswordViewModel : TokenVerificationViewModel
    {
        public string NewPassword { get; set; }
    }
}
