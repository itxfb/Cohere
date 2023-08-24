namespace Cohere.Domain.Service.Nylas
{
    public class NylasCalendar
    {
        public string account_id { get; set; }

        public string description { get; set; }

        public string id { get; set; }

        public bool? is_primary { get; set; }

        public object location { get; set; }

        public string name { get; set; }

        public string @object { get; set; }

        public bool read_only { get; set; }

        public string timezone { get; set; }
    }
}