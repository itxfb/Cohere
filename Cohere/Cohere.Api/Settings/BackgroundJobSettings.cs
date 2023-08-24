namespace Cohere.Api.Settings
{
    public class BackgroundJobSettings
    {
        public int SessionReminderFiresHoursUtc { get; set; }

        public int SessionReminderFireMinutesUtc { get; set; }

        public string TimeZoneToCalculateTomorrowStart { get; set; }
    }
}
