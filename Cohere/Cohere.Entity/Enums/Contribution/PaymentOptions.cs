using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.Contribution
{
    [Newtonsoft.Json.JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentOptions
    {
        EntireCourse = 0,

        PerSession = 1,

        MonthlyMembership = 2,

        SplitPayments = 3,

        SessionsPackage = 4,

        MonthlySessionSubscription = 5,

        DailyMembership = 6,

        WeeklyMembership = 7,

        YearlyMembership = 8,

        MembershipPackage = 9,
        
        Trial = 10,

        Free = 11,
        FreeSessionsPackage = 12
    }
}
