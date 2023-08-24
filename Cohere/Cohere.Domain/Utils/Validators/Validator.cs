using System;
using System.Net.Mail;

namespace Cohere.Domain.Utils.Validators
{
    public static class Email
    {
        public static bool IsValid(string emailaddress)
        {
            try
            {
                MailAddress m = new MailAddress(emailaddress);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}

