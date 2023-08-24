namespace Cohere.Domain.Service
{
    public class ExternalCalendarAccountViewModel
    {
        public string Provider { get; set; }

        public string EmailAddress { get; set; }

        public bool IsCheckConflictsEnabled { get; set; }

        public bool IsEventRemindersEnabled { get; set; }
        public bool IsDefault { get; set; }
    }
}