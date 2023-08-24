using System;
using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Entity.Entities.Contrib
{
    public class ContributionOneToOne : ContributionBase
    {
        public override string Type => nameof(ContributionOneToOne);

        public List<AvailabilityTime> AvailabilityTimes { get; set; }

        public List<int> Durations { get; set; }

        public List<PackagePurchase> PackagePurchases { get; set; } = new List<PackagePurchase>();

        public List<TimeRange> AvailabilityTimesForUi { get; set; } = new List<TimeRange>();

        public OneToOneSessionDataUi OneToOneSessionDataUi { get; set; }

        public string CoachStandardAccountId { get; set; }

        public override List<string> RecordedRooms
        {
            get
            {
                return AvailabilityTimes
                    .SelectMany(at => at.BookedTimes)
                    .SelectMany(bt => bt.RecordingInfos)
                    .Select(e => e.RoomId)
                    .ToList();
            }
        }

        public BookedTime GetBookedTimeById(string bookedTimeId) => AvailabilityTimes?
            .SelectMany(e => e.BookedTimes)
            .FirstOrDefault(e => e.Id == bookedTimeId);

        public Dictionary<string, BookedTimeToAvailabilityTime> GetAvailabilityTimes() => GetAvailabilityTimes(string.Empty);

        public Dictionary<string, BookedTimeToAvailabilityTime> GetAvailabilityTimes(string clientName) => AvailabilityTimes?
            .SelectMany(e => e.BookedTimes
                .Select(b => new BookedTimeToAvailabilityTime()
                {
                    ClientName = clientName,
                    ContributionName = Title,
                    AvailabilityTime = e,
                    BookedTime = b
                })).ToDictionary(key => key.BookedTime.Id) ?? new Dictionary<string, BookedTimeToAvailabilityTime>();

        public override bool IsCompletedTimesChanged(ContributionBase contributionToCheck, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!(contributionToCheck is ContributionOneToOne contributionToCheckOneToOne))
            {
                errorMessage = $"The existed contribution has type {contributionToCheck.Type} which is different from updated contribution type {Type}";
                return true;
            }

            List<BookedTime> existedBookedTimes = GetCompletedBookedTimes(AvailabilityTimes);
            List<BookedTime> bookedTimesToCheck = GetCompletedBookedTimes(contributionToCheckOneToOne.AvailabilityTimes);
            SynchronizeWithExistedData(existedBookedTimes, bookedTimesToCheck);

            var timesEqual = existedBookedTimes.Count == bookedTimesToCheck.Count && existedBookedTimes.All(n => bookedTimesToCheck.Contains(n));

            if (!timesEqual)
            {
                errorMessage = "You try to delete completed sessions";
                return true;
            }

            return false;
        }

        private List<BookedTime> GetCompletedBookedTimes(List<AvailabilityTime> availabilityTimes)
        {
            List<BookedTime> completedBookedTimes = new List<BookedTime>();
            foreach (var time in availabilityTimes)
            {
                completedBookedTimes.AddRange(time.BookedTimes.FindAll(t => t.IsCompleted || t.RecordingInfos.Count != 0));
            }

            return completedBookedTimes;
        }

        private void SynchronizeWithExistedData(List<BookedTime> existedList, List<BookedTime> listToSynchronize)
        {
            var dictToSynchronize = listToSynchronize.ToDictionary(keySelector => keySelector.Id);
            foreach (var time in existedList)
            {
                if (dictToSynchronize.TryGetValue(time.Id, out var targetTime))
                {
                    targetTime.RecordingInfos = time.RecordingInfos;
                }
            }
        }

        public override void CleanSessions()
        {
            throw new NotImplementedException();
        }
    }
}
