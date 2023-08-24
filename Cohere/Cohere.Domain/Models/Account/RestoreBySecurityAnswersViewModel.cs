using System.Collections.Generic;

namespace Cohere.Domain.Models.Account
{
    public class RestoreBySecurityAnswersViewModel
    {
        private string _emailLower;

        public string Email
        {
            get => _emailLower?.ToLower();
            set => _emailLower = value;
        }

        public Dictionary<string, string> SecurityAnswers { get; set; }
    }
}
