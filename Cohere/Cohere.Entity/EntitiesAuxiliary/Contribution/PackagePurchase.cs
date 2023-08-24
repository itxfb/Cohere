using System.Collections.Generic;
using System.Linq;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class PackagePurchase //TODO: need to refactor to reduce complexity
    {
        public string TransactionId { get; set; }

        public int SessionNumbers { get; set; }

        public string UserId { get; set; }

        public bool IsConfirmed { get; set; }

        public bool IsMonthlySessionSubscription { get; set; }

        public int MonthsPaid { get; set; }

        public int SubscriptionDuration { get; set; }

        public Dictionary<string, List<string>> AvailabilityTimeIdBookedTimeIdPairs { get; set; } = new Dictionary<string, List<string>>();

        public int BookedSessionNumbers => AvailabilityTimeIdBookedTimeIdPairs.Values.SelectMany(x => x).Count();

        public int FreeSessionNumbers => SessionNumbers - BookedSessionNumbers;

        public bool IsCompleted => SessionNumbers == BookedSessionNumbers;

        public bool IsMonthlySessionSubscriptionCompleted => IsMonthlySessionSubscription && IsCompleted && MonthsPaid >= SubscriptionDuration;

        public bool IsBookedByPackage(string classId) =>
            AvailabilityTimeIdBookedTimeIdPairs.SelectMany(t => t.Value).Contains(classId);
    }
}
