using Cohere.Entity.Enums.User;

namespace Cohere.Domain.Models.User
{
    public class SwitchFromClientToCoachViewModel
    {
        public string BusinessName { get; set; }

        public string Certification { get; set; }

        public string Occupation { get; set; }

        public BusinessTypes? BusinessType { get; set; }

        public CustomerLabelPreferences? CustomerLabelPreference { get; set; }

        public string CountryId { get; set; }
        public string TimeZoneId { get; set; }
    }
}
