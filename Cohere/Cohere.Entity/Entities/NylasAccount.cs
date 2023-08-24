namespace Cohere.Entity.Entities
{
    public class NylasAccount : BaseEntity
    {
        public string NylasAccountId { get; set; }

        public string CohereAccountId { get; set; }

        public string AccessToken { get; set; }

        public string EmailAddress { get; set; }

        public string Provider { get; set; }

        public string TokenType { get; set; }

        public bool IsCheckConflictsEnabled { get; set; }

        public bool IsEventRemindersEnabled { get; set; }

        public string CalendarId { get; set; }

        public bool IsDefault { get; set; }
    }
}
