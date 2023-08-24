using AutoMapper;
using Cohere.Domain.Extensions;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cohere.Domain.Service.Nylas;
using Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI;

namespace Cohere.Domain.Service
{
    public class ContributionRootService : IContributionRootService
    {
        private const string GuidDateFormat = "%M/%d/yyyy h:mm:ss tt";
        private readonly IUnitOfWork _unitOfWork;
        private readonly NylasService _nylasService;
        private readonly IMapper _mapper;

        public ContributionRootService(IUnitOfWork unitOfWork, NylasService nylasService, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _nylasService = nylasService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<AvailabilityTime>> GetAvailabilityTimesForCoach(string contributionId, int offset, OneToOneSessionDataUi schedulingCriteria = default, bool timesInUtc = false)
        {
            var contribution = await GetOne(contributionId);

            if (contribution is ContributionOneToOne contributionOneToOne)
            {
                var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contributionOneToOne.UserId);
                var coachAccountId = coachUser.AccountId;
                var scheduledSlots = await GetAvailabilityTimes(
                    coachAccountId: coachAccountId,
                    schedulingCriteria: schedulingCriteria ?? contributionOneToOne.OneToOneSessionDataUi,
                    existedAvailabilityTimes: contributionOneToOne.AvailabilityTimes.Where(e => e.BookedTimes.Any()).ToList(),
                    offset: offset);
                var allSlots = scheduledSlots
                    .Concat(contributionOneToOne.AvailabilityTimes.Where(e => e.BookedTimes.Any()).ToList())
                    .OrderBy(e => e.StartTime).ToArray();

                AssignIds(contributionId, allSlots);

                if (!timesInUtc)
                {
                    ConvertToTimezone(allSlots, coachUser.TimeZoneId);
                }

                return allSlots;
            }
            else
            {
                return new List<AvailabilityTime>();
            }
        }

        public async Task<IEnumerable<AvailabilityTime>> GetAvailabilityTimesForClient(string contributionId, string clientAccountId, int offset, string timezoneId, bool timesInUtc = false, bool withTimeZoneId = false)
        {
            var contribution = await GetOne(contributionId);

            if (contribution is ContributionOneToOne contributionOneToOne)
            {
                var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contributionOneToOne.UserId);

                var scheduledSlots = await GetAvailabilityTimes(
                    coachAccountId: coachUser.AccountId,
                    schedulingCriteria: contributionOneToOne.OneToOneSessionDataUi,
                    existedAvailabilityTimes: contributionOneToOne.AvailabilityTimes.Where(e => e.BookedTimes.Any()).ToList(),
                    offset: offset);

                var allSlots = scheduledSlots
                    .Concat(contributionOneToOne.AvailabilityTimes.Where(e => e.BookedTimes.Any()).ToList())
                    .OrderBy(e => e.StartTime).ToArray();
                
                AssignIds(contributionId, allSlots);

                var clientUser = !string.IsNullOrEmpty(clientAccountId) ? await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == clientAccountId) : default;
                allSlots.CleanClientInfo(clientUser?.Id);

                if (withTimeZoneId)
                {
                    ConvertToTimezone(allSlots, timezoneId);
                }
                else if (!timesInUtc)
                {
                    ConvertToTimezone(allSlots, clientUser?.TimeZoneId ?? coachUser.TimeZoneId);
                }

                return allSlots;
            }
            else
            {
                return new List<AvailabilityTime>();
            }
        }

        private static void ConvertToTimezone(IEnumerable<AvailabilityTime> allSlots, string coachTimezoneId)
        {
            foreach (var slot in allSlots)
            {
                slot.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(slot.StartTime, coachTimezoneId);
                slot.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(slot.EndTime, coachTimezoneId);

                foreach (var bookedTimes in slot.BookedTimes)
                {
                    bookedTimes.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(bookedTimes.StartTime, coachTimezoneId);
                    bookedTimes.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(bookedTimes.EndTime, coachTimezoneId);
                }
            }
        }

        public async Task<IEnumerable<AvailabilityTime>> CalculateSlots(string coachAccountId, OneToOneSessionDataUi schedulingCriteria, bool timesInUtc = false)
        {
            var allSlots = (await GetAvailabilityTimes(
                coachAccountId: coachAccountId,
                schedulingCriteria: schedulingCriteria,
                existedAvailabilityTimes: new List<AvailabilityTime>(),
                offset: 0)).ToArray();
            
            if (timesInUtc)
            {
                return allSlots;
            }
            
            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == coachAccountId);
            ConvertToTimezone(allSlots, coachUser.TimeZoneId);

            return allSlots;
        }

        private void AssignIds(string contirbutionId, AvailabilityTime[] allSlots)
        {
            var slotsWithoutId = allSlots.Where(e => string.IsNullOrEmpty(e.Id));

            foreach (var slot in slotsWithoutId)
            {
                var input = $"{contirbutionId}/{slot.StartTime.ToString(GuidDateFormat)}/{slot.EndTime.ToString(GuidDateFormat)}";
                var newId = GuidUtility.Create(GuidUtility.UrlNamespace, input).ToString();

                while (allSlots.Any(e => e.Id == newId))
                {
                    input += "/rescheduled";
                    newId = GuidUtility.Create(GuidUtility.UrlNamespace, input).ToString();
                }

                slot.Id = newId;
            }
        }

        public async Task<ContributionBase> GetOne(string contributionId)
        {
            return await GetOne(e => e.Id == contributionId);
        }

        public async Task<ContributionBase> GetOne(Expression<Func<ContributionBase, bool>> predicate)
        {
            return await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(predicate);
        }

        public async Task<IEnumerable<ContributionBase>> Get(Expression<Func<ContributionBase, bool>> predicate)
        {
            return await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(predicate);
        }
        public async Task<IEnumerable<ContributionBase>> GetSkipTake(Expression<Func<ContributionBase, bool>> predicate, int skip, int take)
        {
            return await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetSkipTake(predicate, skip, take);
        }
        public async Task<IEnumerable<ContributionBase>> GetSkipTakeWithSort(Expression<Func<ContributionBase, bool>> predicate, int skip, int take, OrderByEnum orderByEnum)
        {
            return await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetSkipTakeWithSort(predicate, skip, take, orderByEnum);
        }
        public async Task<int> GetCount(Expression<Func<ContributionBase, bool>> predicate)
        {
            return await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetCount(predicate);
        }
        public async Task<IEnumerable<CohealerContributionTimeRangeViewModel>> GetCohealerContributionsTimeRangesForCohealer(string cohealerAccountId, bool timesInUtc = false)
        {
            var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == cohealerAccountId);
            var contributions = await Get(c => c.UserId == contributor.Id && c.Status == ContributionStatuses.Approved);

            var oneToOneContributions = contributions
                .Where(contribution => contribution is ContributionOneToOne)
                .Select(e => e as ContributionOneToOne)
                .SelectMany(e => e.AvailabilityTimes)
                .SelectMany(e => e.BookedTimes)
                .Select(e => e.ParticipantId)
                .Distinct().ToHashSet();

            var clients = (await _unitOfWork.GetRepositoryAsync<User>().Get(e => oneToOneContributions.Contains(e.Id)))
                .ToDictionary(e => e.Id, client => $"{client.FirstName} {client.LastName}");

            var contributionViewModels = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(contributions);

            return contributionViewModels.SelectMany(c => c.GetCohealerContributionTimeRanges(clients, contributor.TimeZoneId, timesInUtc));
        }

        private async Task<IEnumerable<AvailabilityTime>> GetAvailabilityTimes(string coachAccountId, OneToOneSessionDataUi schedulingCriteria, List<AvailabilityTime> existedAvailabilityTimes, int offset)
        {
            if (schedulingCriteria == null)
            {
                return new List<AvailabilityTime>();
            }

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == coachAccountId);
            var coachTimeZoneId = coachUser.TimeZoneId;


            var scheduledSlotsInUtc = SlotsGenerator.GetScheduledSlots(schedulingCriteria, coachTimeZoneId, offset)?.ToArray();
            
            if (scheduledSlotsInUtc is null || !scheduledSlotsInUtc.Any())
            {
                return new List<AvailabilityTime>();
            }

            var from = scheduledSlotsInUtc.Min(e => e.StartTime);
            var end = scheduledSlotsInUtc.Max(e => e.EndTime);

            var busyTimesInUtc = await GetBusyTimeFromCalendar(coachUser.AccountId, from, end);

            var otherContributionSessions = await GetCohealerContributionsTimeRangesForCohealer(coachUser.AccountId, true);

            var busyTimesFromOtherContributions = otherContributionSessions.Select(e => new TimeRange
            {
                StartTime = e.SessionStartTime,
                EndTime = e.SessionEndTime
            });

            var notAvailableTime = busyTimesInUtc
                .Concat(busyTimesFromOtherContributions)
                .Concat(existedAvailabilityTimes ?? new List<AvailabilityTime>());

            return RemoveOverlappings(scheduledSlotsInUtc, notAvailableTime);
        }

        private async Task<List<TimeRange>> GetBusyTimeFromCalendar(string accountId, DateTime from, DateTime end)
        {
            var accountToCheckConflictResult = await _nylasService.GetNylasAccountsWithCheckConflictsEnabledForCohereAccountAsync(accountId);

            if (!accountToCheckConflictResult.Succeeded)
            {
                return new List<TimeRange>();
            }
            
            var coachCalendarBusyTimeResult = await _nylasService.GetBusyTimesInUtc(accountId, new DateTimeOffset(@from), new DateTimeOffset(end));
            
            return coachCalendarBusyTimeResult.Succeeded ? coachCalendarBusyTimeResult.Payload : new List<TimeRange>();
        }

        private IEnumerable<AvailabilityTime> RemoveOverlappings(IEnumerable<AvailabilityTime> scheduledSlotsInUtc, IEnumerable<TimeRange> busyTimesInUtc)
        {
            var result = new List<AvailabilityTime>();
            foreach (var slot in scheduledSlotsInUtc)
            {
                if (!busyTimesInUtc.Any(busyTime => SlotsGenerator.CheckForOverlapping(slot, busyTime)))
                {
                    result.Add(slot);
                }
            }
            return result;
        }
    }


    public class SlotsGenerator
    {
        private const string CustomDuration = "custom";

        public static IEnumerable<AvailabilityTime> GetScheduledSlots(OneToOneSessionDataUi schedulingCriteria, string coachTimeZoneId, int offset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            var zonedNow = DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow, coachTimeZoneId);

            DateTime start = default;
            DateTime end = default;

            if (schedulingCriteria.Duration == CustomDuration)
            {
                start = schedulingCriteria.StartDay.Date;// > zonedNow.Date ? schedule.StartDay.Date : zonedNow.Date;
                end = schedulingCriteria.EndDay.Date;
            }
            else if (int.TryParse(schedulingCriteria.Duration, out var duration))
            {
                start = zonedNow.Date;
                end = start.AddDays(duration);
            }
            else
            {
                throw new ArgumentException("can't obtain session duration");
            }

            if ((end - start).Days < 0)
            {
                return new AvailabilityTime[0];
            }

            var coachStartOfDayInUtc = DateTimeHelper.GetUtcTimeFromZoned(zonedNow.Date, coachTimeZoneId);

            return GetSlotsForWeeks(
                DateTime.SpecifyKind(start, DateTimeKind.Unspecified),
                DateTime.SpecifyKind(end, DateTimeKind.Unspecified),
                schedulingCriteria.SelectedWeeks,
                schedulingCriteria.SessionDuration,
                offset,
                coachTimeZoneId)
                .OrderBy(e => e.StartTime)
                .Where(e => e.StartTime >= coachStartOfDayInUtc);
        }

        private static IEnumerable<AvailabilityTime> GetSlotsForWeeks(
            DateTime startDate,
            DateTime endDate,
            List<SelectedWeek> selectedWeeks,
            int durationInMinutes,
            int sessionOffsetInMinutes,
            string coachTimeZoneId)
        {
            var sessionDuration = TimeSpan.FromMinutes(durationInMinutes);
            var result = new List<AvailabilityTime>();
            foreach (var week in selectedWeeks)
            {
                result.AddRange(GetSlotForWeek(startDate, endDate, week, sessionDuration, sessionOffsetInMinutes, coachTimeZoneId));
            }

            return RemoveOverlappings(result);
        }

        private static IEnumerable<AvailabilityTime> GetSlotForWeek(
            DateTime startDate,
            DateTime endDate,
            SelectedWeek week,
            TimeSpan sessionDuration,
            int sessionOffsetInMinutes,
            string coachTimeZoneId)
        {
            var result = new List<AvailabilityTime>();

            foreach (var day in week.Days)
            {
                result.AddRange(
                    GetSlotForDay(
                        startDate,
                        endDate,
                        day.Value,
                        week.StartTime,
                        week.EndTime,
                        sessionDuration,
                        sessionOffsetInMinutes,
                        coachTimeZoneId)
                );
            }

            return result;
        }

        private static IEnumerable<AvailabilityTime> GetSlotForDay(
            DateTime startDate,
            DateTime endDate,
            string dayOfWeek,
            DateTime availabilityStartTimeInCoachTimeZone,
            DateTime availabilityEndTimeInCoachTimeZone,
            TimeSpan sessionDuration,
            int sessionOffsetInMinutes,
            string coachTimeZoneId)
        {
            TimeSpan sessionOffset = TimeSpan.FromMinutes(sessionOffsetInMinutes);

            TimeSpan timeRange = GetTimeRange(availabilityStartTimeInCoachTimeZone, availabilityEndTimeInCoachTimeZone, sessionOffset);

            var sessionsCount = (int)Math.Truncate(timeRange / sessionDuration);

            if (sessionsCount < 1)
            {
                return new List<AvailabilityTime>();//not able to generate any slot for this time period
            }

            var time = GetTimeWithoutSecond(availabilityStartTimeInCoachTimeZone) + sessionOffset;

            var availabilitiesStartDateTime = DaysInTimeRange(startDate, endDate, GetDay(dayOfWeek)).Select(e => e + time);

            return availabilitiesStartDateTime
                .SelectMany(availabilityTimeStart =>
                    GetSlotsForAvailabilityTime(sessionDuration, sessionOffsetInMinutes, coachTimeZoneId, availabilityTimeStart, sessionsCount))
                .ToList();
        }

        private static IEnumerable<AvailabilityTime> GetSlotsForAvailabilityTime(
            TimeSpan sessionDuration,
            int sessionOffsetInMinutes,
            string coachTimeZoneId,
            DateTime availabilityTimeStart,
            int sessionsCount)
        {
            var result = new List<AvailabilityTime>();

            foreach (var sessionNumber in Enumerable.Range(0, sessionsCount))
            {
                var startTime = availabilityTimeStart + (sessionDuration * sessionNumber);

                if (DateTimeHelper.TryGetUtcTimeFromZoned(startTime, coachTimeZoneId, out var zonedStartTime)
                    && DateTimeHelper.TryGetUtcTimeFromZoned(startTime + sessionDuration, coachTimeZoneId, out var zonedEndTime))
                {
                    result.Add(new AvailabilityTime()
                    {
                        StartTime = zonedStartTime,
                        EndTime = zonedEndTime,
                        Offset = sessionOffsetInMinutes
                    });
                }
            }

            return result;
        }

        private static TimeSpan GetTimeRange(DateTime availabilityStartTimeInCoachTimeZone, DateTime availabilityEndTimeInCoachTimeZone, TimeSpan sessionOffset)
        {
            var timeRange = GetTimeWithoutSecond(availabilityEndTimeInCoachTimeZone) - GetTimeWithoutSecond(availabilityStartTimeInCoachTimeZone);
            if (timeRange < TimeSpan.Zero)
            {
                timeRange = timeRange + TimeSpan.FromDays(1);
            }

            timeRange -= sessionOffset;
            return timeRange;
        }

        private static TimeSpan GetTimeWithoutSecond(DateTime dateTime)
        {
            return new TimeSpan(dateTime.TimeOfDay.Hours, dateTime.TimeOfDay.Minutes, 0);
        }

        private static DayOfWeek GetDay(string dayShortcut)
        {
            switch (dayShortcut.ToUpper())
            {
                case "MON":
                    return DayOfWeek.Monday;
                case "TUE":
                    return DayOfWeek.Tuesday;
                case "WED":
                    return DayOfWeek.Wednesday;
                case "THU":
                    return DayOfWeek.Thursday;
                case "FRI":
                    return DayOfWeek.Friday;
                case "SAT":
                    return DayOfWeek.Saturday;
                case "SUN":
                    return DayOfWeek.Sunday;
                default:
                    throw new ArgumentException("not supported day");
            }
        }
        public static bool CheckForOverlapping(TimeRange slot, TimeRange busyTime)
        {
            return (slot.StartTime <= busyTime.EndTime && slot.EndTime >= busyTime.StartTime) &&
                (slot.StartTime != busyTime.EndTime && slot.EndTime != busyTime.StartTime);//close overlappings allowed
        }

        private static IEnumerable<DateTime> DaysInTimeRange(DateTime startDateInCoachTimeZone, DateTime endDateInCoachTimeZone, DayOfWeek dayOfWeek)
        {
            if (startDateInCoachTimeZone > endDateInCoachTimeZone)
            {
                throw new ArgumentException("start day is greater than end day");
            }

            return GetAllDaysBetween(startDateInCoachTimeZone.Date, endDateInCoachTimeZone.Date).Where(e => e.DayOfWeek == dayOfWeek);
        }

        private static IEnumerable<DateTime> GetAllDaysBetween(DateTime start, DateTime end)
        {
            if (start > end)
            {
                throw new ArgumentException("start day is greater than end day");
            }

            var dateDiff = (end - start).Days;

            return Enumerable.Range(0, dateDiff + 1).Select(e => start.AddDays(e));
        }

        private static IEnumerable<AvailabilityTime> RemoveOverlappings(IEnumerable<AvailabilityTime> scheduledSlotsInUtc)
        {
            var result = new List<AvailabilityTime>();
            foreach (var slot in scheduledSlotsInUtc)
            {
                if (!scheduledSlotsInUtc.Any(busyTime => busyTime != slot && CheckForOverlapping(slot, busyTime)))
                {
                    result.Add(slot);
                }
            }
            return result;
        }
    }



    /// <summary>
    /// Helper methods for working with <see cref="Guid"/>.
    /// </summary>
    /// <remarks>See <a href="https://github.com/LogosBible/Logos.Utility/blob/master/src/Logos.Utility/GuidUtility.cs">source</a>.</remarks>
    public static class GuidUtility
    {
        /// <summary>
        /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
        /// </summary>
        /// <param name="namespaceId">The ID of the namespace.</param>
        /// <param name="name">The name (within that namespace).</param>
        /// <returns>A UUID derived from the namespace and name.</returns>
        public static Guid Create(Guid namespaceId, string name)
        {
            return Create(namespaceId, name, 5);
        }

        /// <summary>
        /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
        /// </summary>
        /// <param name="namespaceId">The ID of the namespace.</param>
        /// <param name="name">The name (within that namespace).</param>
        /// <param name="version">The version number of the UUID to create; this value must be either
        /// 3 (for MD5 hashing) or 5 (for SHA-1 hashing).</param>
        /// <returns>A UUID derived from the namespace and name.</returns>
        public static Guid Create(Guid namespaceId, string name, int version)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (version != 3 && version != 5)
                throw new ArgumentOutOfRangeException("version", "version must be either 3 or 5.");

            // convert the name to a sequence of octets (as defined by the standard or conventions of its namespace) (step 3)
            // ASSUME: UTF-8 encoding is always appropriate
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);

            // convert the namespace UUID to network order (step 3)
            byte[] namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            // comput the hash of the name space ID concatenated with the name (step 4)
            byte[] hash;
            using (HashAlgorithm algorithm = version == 3 ? (HashAlgorithm)MD5.Create() : SHA1.Create())
            {
                algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                hash = algorithm.Hash;
            }

            // most bytes from the hash are copied straight to the bytes of the new GUID (steps 5-7, 9, 11-12)
            byte[] newGuid = new byte[16];
            Array.Copy(hash, 0, newGuid, 0, 16);

            // set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
            newGuid[6] = (byte)((newGuid[6] & 0x0F) | (version << 4));

            // set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively (step 10)
            newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

            // convert the resulting UUID to local byte order (step 13)
            SwapByteOrder(newGuid);
            return new Guid(newGuid);
        }

        /// <summary>
        /// The namespace for fully-qualified domain names (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>
        /// The namespace for URLs (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>
        /// The namespace for ISO OIDs (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid IsoOidNamespace = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

        // Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
        internal static void SwapByteOrder(byte[] guid)
        {
            SwapBytes(guid, 0, 3);
            SwapBytes(guid, 1, 2);
            SwapBytes(guid, 4, 5);
            SwapBytes(guid, 6, 7);
        }

        private static void SwapBytes(byte[] guid, int left, int right)
        {
            byte temp = guid[left];
            guid[left] = guid[right];
            guid[right] = temp;
        }
    }
}
