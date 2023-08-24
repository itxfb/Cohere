using Cohere.Domain.Utils;
using Nager.Country;
using NUnit.Framework;
using System.Linq;

namespace Cohere.Api.UnitTests
{
    public class CountryCodeByTimezone
    {
        private static readonly string[] StripeSupportedCountries = new string[] {
            "Australia",
            "Austria",
            "Belgium",
            "Brazil",
            "Bulgaria",
            "Canada",
            "Cyprus",
            "Czech Republic",
            "Denmark",
            "Estonia",
            "Finland",
            "France",
            "Germany",
            "Greece",
            "Hong Kong",
            "Hungary",
            "India",
            "Ireland",
            "Italy",
            "Japan",
            "Latvia",
            "Lithuania",
            "Luxembourg",
            "Malta",
            "Mexico",
            "Netherlands",
            "New Zealand",
            "Norway",
            "Poland",
            "Portugal",
            "Romania",
            "Singapore",
            "Slovakia",
            "Slovenia",
            "Spain",
            "Sweden",
            "Switzerland",
            "Britain (UK)",
            "United States"
        };

        //[Test]
        //public void CheckStripeSupportedCountries()
        //{
        //    ICountryProvider countryProvider = new CountryProvider();
        //    var alpha2Codes = countryProvider.GetCountries().Select(e => e.Alpha2Code.ToString()).ToList();

        //    foreach (var timeZone in DateTimeHelper.TimeZoneFriendlyNames)
        //    {
        //        var location = DateTimeHelper.GetCountryInfoByIanaTimeZone(timeZone.Key);
        //        Assert.IsNotNull(location, "TimeZone location is ambiguous");

        //        Assert.Contains(location.CountryCode, alpha2Codes, "location code not supported by Stripe");

        //        Assert.Contains(location.CountryName, StripeSupportedCountries, "location country not supported by Stripe");
        //    }
        //}
    }
}
