using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeoTimeZone;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.TimeZones;
using TimeZoneConverter;

namespace Cohere.Domain.Utils
{
    public static class DateTimeHelper
    {
        // TODO Get FirstDayOfWeek from request culture instead from machine culture once have localization added
        public static DateTime StartOfWeek =>
            DateTime.SpecifyKind(DateTime.Today.AddDays((int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek - (int)DateTime.Today.DayOfWeek), DateTimeKind.Utc);

        public static DateTime StartOfNextWeek => StartOfWeek.AddDays(7);

        public static DateTime StartOfMonth => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        public static DateTime StartOfNextMonth => StartOfMonth.AddMonths(1);

        public static DateTime StartOfLastMonth => StartOfMonth.AddMonths(-1);

        public static DateTime EndOfNextMonth => StartOfNextMonth.AddMonths(1);

        public static DateTime StartOfYear => new DateTime(DateTime.UtcNow.Year, 1, 1);

        public static DateTime EndOfYear => new DateTime(DateTime.UtcNow.Year, 12, 31, 23, 59, 59);

        public static string CalculateTimeZoneIanaId(double latitude, double longitude)
        {
            var timeZoneIanaResult = TimeZoneLookup.GetTimeZone(latitude, longitude);
            return timeZoneIanaResult.Result;
        }

        public static string CalculateTimeZoneWindowsId(double latitude, double longitude)
        {
            var timeZoneIanaResult = TimeZoneLookup.GetTimeZone(latitude, longitude);
            return TZConvert.IanaToWindows(timeZoneIanaResult.Result);
        }

        public static IEnumerable<string> GetAllIanaTimeZoneNames()
        {
            return DateTimeZoneProviders.Tzdb.GetAllZones().Select(t => t.Id);
        }

        public static DateTime GetZonedDateTimeFromUtc(DateTime utcDateTime, string ianaTimeZoneId)
        {
            var timeZone = DateTimeZoneProviders.Tzdb[ianaTimeZoneId];
            var instantTime = utcDateTime.ToInstant();
            return instantTime.InZone(timeZone).ToDateTimeUnspecified();
        }

        public static DateTime GetUtcTimeFromZoned(DateTime dateTime, string ianaTimeZoneId)
        {
            var dateTimeZone = DateTimeZoneProviders.Tzdb[ianaTimeZoneId];
            var localDateTime = LocalDateTime.FromDateTime(dateTime);
            var zonedDateTime = dateTimeZone.AtStrictly(localDateTime);

            return zonedDateTime.ToInstant().ToDateTimeUtc();
        }

        public static bool TryGetUtcTimeFromZoned(DateTime dateTime, string ianaTimeZoneId, out DateTime result)
        {
            try
            {
                var dateTimeZone = DateTimeZoneProviders.Tzdb[ianaTimeZoneId];
                var localDateTime = LocalDateTime.FromDateTime(dateTime);
                var zonedDateTime = dateTimeZone.AtStrictly(localDateTime);

                result = zonedDateTime.ToInstant().ToDateTimeUtc();
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                result = default;
                return false;
            }
        }

        public static readonly Dictionary<string, string> TimeZoneFriendlyNames = new Dictionary<string, string>
            {
                { "America/Los_Angeles", "Pacific Standard Time" },
                { "America/Denver", "Mountain Standard Time" },
                { "America/Chicago", "Central Standard Time" },
                { "America/Costa_Rica", "Central Standard Time"},
                { "America/New_York", "Eastern Standard Time" },
                { "America/Anchorage", "Alaskan Standard Time" },
                { "America/St_Johns", "Newfoundland Standard Time" },
                { "Pacific/Honolulu", "Hawaiian Standard Time" },
                { "America/Phoenix", "Arizona Standard Time" },
                { "Australia/ACT", "Australia/ACT" },
                { "Australia/Adelaide", "Australia/Adelaide" },
                { "Australia/Brisbane", "Australia/Brisbane" },
                { "Australia/Broken_Hill", "Australia/Broken_Hill" },
                { "Australia/Canberra", "Australia/Canberra" },
                { "Australia/Currie", "Australia/Currie" },
                { "Australia/Darwin", "Australia/Darwin" },
                { "Australia/Eucla", "Australia/Eucla" },
                { "Australia/Hobart", "Australia/Hobart" },
                { "Australia/LHI", "Australia/LHI" },
                { "Australia/Lindeman", "Australia/Lindeman" },
                { "Australia/Lord_Howe", "Australia/Lord_Howe" },
                { "Australia/Melbourne", "Australia/Melbourne" },
                { "Australia/North", "Australia/North" },
                { "Australia/NSW", "Australia/NSW" },
                { "Australia/Perth", "Australia/Perth" },
                { "Australia/Queensland", "Australia/Queensland" },
                { "Australia/South", "Australia/South" },
                { "Australia/Sydney", "Australia/Sydney" },
                { "Australia/Tasmania", "Australia/Tasmania" },
                { "Australia/Victoria", "Australia/Victoria" },
                { "Australia/West", "Australia/West" },
                { "Australia/Yancowinna", "Australia/Yancowinna" },
                { "Canada/Atlantic", "Canada/Atlantic" },
                { "Canada/Central", "Canada/Central" },
                { "Canada/Eastern", "Canada/Eastern" },
                { "Canada/Mountain", "Canada/Mountain" },
                { "Canada/Newfoundland", "Canada/Newfoundland" },
                { "Canada/Pacific", "Canada/Pacific" },
                { "Canada/Saskatchewan", "Canada/Saskatchewan" },
                { "Canada/Yukon", "Canada/Yukon" },
                { "Europe/Amsterdam", "Europe/Amsterdam" },
                { "Europe/Andorra", "Europe/Andorra" },
                { "Europe/Athens", "Europe/Athens" },
                { "Europe/Belfast", "Europe/Belfast" },
                { "Europe/Belgrade", "Europe/Belgrade" },
                { "Europe/Berlin", "Europe/Berlin" },
                { "Europe/Bratislava", "Europe/Bratislava" },
                { "Europe/Brussels", "Europe/Brussels" },
                { "Europe/Bucharest", "Europe/Bucharest" },
                { "Europe/Budapest", "Europe/Budapest" },
                { "Europe/Busingen", "Europe/Busingen" },
                { "Europe/Chisinau", "Europe/Chisinau" },
                { "Europe/Copenhagen", "Europe/Copenhagen" },
                { "Europe/Dublin", "Europe/Dublin" },
                { "Europe/Gibraltar", "Europe/Gibraltar" },
                { "Europe/Guernsey", "Europe/Guernsey" },
                { "Europe/Helsinki", "Europe/Helsinki" },
                { "Europe/Isle_of_Man", "Europe/Isle of Man" },
                { "Europe/Istanbul", "Europe/Istanbul" },
                { "Europe/Jersey", "Europe/Jersey" },
                { "Europe/Kaliningrad", "Europe/Kiev" },
                { "Europe/Lisbon", "Europe/Lisbon" },
                { "Europe/Ljubljana", "Europe/Ljubljana" },
                { "Europe/London", "Europe/London" },
                { "Europe/Luxembourg", "Europe/Luxembourg" },
                { "Europe/Madrid", "Europe/Madrid" },
                { "Europe/Malta", "Europe/Malta" },
                { "Europe/Mariehamn", "Europe/Mariehamn" },
                { "Europe/Minsk", "Europe/Minsk" },
                { "Europe/Monaco", "Europe/Monaco" },
                { "Europe/Moscow", "Europe/Moscow" },
                { "Europe/Nicosia", "Europe/Nicosia" },
                { "Europe/Oslo", "Europe/Oslo" },
                { "Europe/Paris", "Europe/Paris" },
                { "Europe/Podgorica", "Europe/Podgorica" },
                { "Europe/Prague", "Europe/Prague" },
                { "Europe/Riga", "Europe/Riga" },
                { "Europe/Rome", "Europe/Rome" },
                { "Europe/Samara", "Europe/Samara" },
                { "Europe/San_Marino", "Europe/San_Marino" },
                { "Europe/Sarajevo", "Europe/Sarajevo" },
                { "Europe/Simferopol", "Europe/Simferopol" },
                { "Europe/Skopje", "Europe/Skopje" },
                { "Europe/Sofia", "Europe/Sofia" },
                { "Europe/Stockholm", "Europe/Stockholm" },
                { "Europe/Tallinn", "Europe/Tallinn" },
                { "Europe/Tirane", "Europe/Tirane" },
                { "Europe/Tiraspol", "Europe/Tiraspol" },
                { "Europe/Uzhgorod", "Europe/Uzhgorod" },
                { "Europe/Vaduz", "Europe/Vaduz" },
                { "Europe/Vatican", "Europe/Vatican" },
                { "Europe/Vienna", "Europe/Vienna" },
                { "Europe/Vilnius", "Europe/Vilnius" },
                { "Europe/Volgograd", "Europe/Volgograd" },
                { "Europe/Warsaw", "Europe/Warsaw" },
                { "Europe/Zagreb", "Europe/Zagreb" },
                { "Europe/Zaporozhye", "Europe/Zaporozhye" },
                { "Europe/Zurich", "Europe/Zurich" },
                { "Hongkong", "Hongkong" },
                { "Iceland", "Iceland" },
                { "Israel", "Israel" },
                { "Japan", "Japan" },
                { "Mexico/BajaNorte", "Mexico/Pacific" },
                { "Mexico/BajaSur", "Mexico/Mountain" },
                { "Mexico/General", "Mexico/Central" },
                { "Navajo", "Navajo" },
                { "NZ", "NZ" },
                { "NZ-CHAT", "NZ-CHAT" },
                { "Singapore", "Singapore" },
                { "Asia/Dubai", "Gulf Standard Time" },
                { "America/Bogota", "Colombia Standard Time" },
                { "Asia/Calcutta", "Asia/Calcutta" },
                {"Asia/Kolkata", "Asia/Kolkata" }
            };

        // todo: remove; was commented out because we are using values from database now (TimeZone Collection)
        //public static TzdbZoneLocation GetCountryInfoByIanaTimeZone(string ianaTimeZoneId)
        //{
        //    var source = TzdbDateTimeZoneSource.Default;
        //    return source.ZoneLocations.FirstOrDefault(loc => loc.ZoneId == source.CanonicalIdMap[ianaTimeZoneId]);
        //}
    }
}
