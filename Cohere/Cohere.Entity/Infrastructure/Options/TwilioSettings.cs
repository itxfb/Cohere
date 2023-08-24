namespace Cohere.Entity.Infrastructure.Options
{
    public class TwilioSettings
    {
        public string TwilioAccountSid { get; set; }

        public string TwilioApiSid { get; set; }

        public string ChatServiceSid { get; set; }

        public string ChatUserRoleSid { get; set; }

        public int ChatTokenLifetimeSec { get; set; }

        public int VideoTokenLifetimeSec { get; set; }

        public string VideoWebHookUrl { get; set; }

        public string ContributionWebHookUrl { get; set; }
    }
}
