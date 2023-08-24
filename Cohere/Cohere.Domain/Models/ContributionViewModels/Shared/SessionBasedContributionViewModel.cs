using System;
using System.Collections.Generic;
using System.Linq;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.User;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Utils;
using FluentValidation;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public abstract class SessionBasedContributionViewModel : ContributionBaseViewModel
    {
        public SessionBasedContributionViewModel(IValidator validator) : base(validator)
        {

        }

        public List<Session> Sessions { get; set; } = new List<Session>();

        public Enrollment Enrollment { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public override List<TimeRange> CohealerBusyTimeRanges
        {
            get
            {
                BusyTimeRanges = Sessions.SelectMany(s => s.SessionTimes)
                    .Select(st => new TimeRange { StartTime = st.StartTime, EndTime = st.EndTime }).ToList();
                return BusyTimeRanges;
            }
            set => BusyTimeRanges = value;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, ClassInfo> ClassesInfo =>
            Sessions
                .SelectMany(st => st.SessionTimes)
                .ToDictionary(
                    key => key.Id,
                    value => new ClassInfo
                    {
                        IsCompleted = value.IsCompleted,
                        ParticipantIds = Pods.FirstOrDefault(x => x.Id == value.PodId)?.ClientIds == null
                            ? value.ParticipantsIds
                            : value.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == value.PodId)?.ClientIds).ToList(),
                        StartTime = value.StartTime,
                        RecordingInfos = value.RecordingInfos,
                        VideoRoomContainer = value,
                    });

        private bool HasIncompletedSession => Sessions?.Any(e => !e.IsCompleted && !e.IsPrerecorded) ?? false;

        public override bool ArchivingAllowed => !HasIncompletedSession;
        private bool IsCompleted => Status == ContributionStatuses.Completed.ToString();

        [System.Text.Json.Serialization.JsonIgnore]
        public List<Pod> Pods { get; set; } = new List<Pod>();

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, List<string>> RoomsWithParticipants =>
            Sessions
                .SelectMany(s => s.SessionTimes
                    .SelectMany(st => st.RecordingInfos
                        .Select(ri => new KeyValuePair<string, List<string>>(ri.RoomId, Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds == null
                            ? st.ParticipantsIds
                            : st.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds).ToList()))))
                .ToDictionary(x => x.Key, x => x.Value);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<Document> Attachments =>
            Sessions.SelectMany(s => s.Attachments);

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, IList<Document>> AttachmentCollection =>
            Sessions.Select(s => new KeyValuePair<string, IList<Document>>(s.Id, s.Attachments))
                .ToDictionary(e => e.Key, e => e.Value);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<string> AttachmentsKeys =>
            Attachments.Select(e => e.DocumentKeyWithExtension);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<VideoRoomInfo> VideoRoomInfos =>
            Sessions
                .SelectMany(s => s.SessionTimes)
                .Select(e => e.VideoRoomInfo);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<RecordingInfo> RecordingInfos =>
            Sessions
                .SelectMany(e => e.SessionTimes)
                .SelectMany(e => e.RecordingInfos);

        public override void ConvertAllOwnZonedTimesToUtc(string timeZoneId)
        {
            foreach (var session in Sessions)
            {
                if (session.CompletedDateTime.HasValue)
                {
                    session.CompletedDateTime =
                        DateTimeHelper.GetUtcTimeFromZoned(session.CompletedDateTime.Value, timeZoneId);
                }

                foreach (var sessionTime in session.SessionTimes)
                {
                    sessionTime.StartTime = DateTimeHelper.GetUtcTimeFromZoned(sessionTime.StartTime, timeZoneId);
                    sessionTime.EndTime = DateTimeHelper.GetUtcTimeFromZoned(sessionTime.EndTime, timeZoneId);
                    if (sessionTime.CompletedDateTime.HasValue)
                    {
                        sessionTime.CompletedDateTime =
                            DateTimeHelper.GetUtcTimeFromZoned(sessionTime.CompletedDateTime.Value, timeZoneId);
                    }
                }
            }

            if (Enrollment != null)
            {
                Enrollment.FromDate = DateTimeHelper.GetUtcTimeFromZoned(Enrollment.FromDate, timeZoneId);
                Enrollment.ToDate = DateTimeHelper.GetUtcTimeFromZoned(Enrollment.ToDate, timeZoneId);
            }
        }

        public override void ConvertAllOwnUtcTimesToZoned(string timeZoneId)
        {
            foreach (var session in Sessions)
            {
                if (session.CompletedDateTime.HasValue)
                {
                    session.CompletedDateTime =
                        DateTimeHelper.GetZonedDateTimeFromUtc(session.CompletedDateTime.Value, timeZoneId);
                }

                foreach (var sessionTime in session.SessionTimes)
                {
                    sessionTime.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, timeZoneId);
                    sessionTime.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.EndTime, timeZoneId);

                    if (sessionTime.CompletedDateTime.HasValue)
                    {
                        sessionTime.CompletedDateTime =
                            DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.CompletedDateTime.Value, timeZoneId);
                    }
                }
            }

            if (Enrollment != null)
            {
                Enrollment.FromDate = DateTimeHelper.GetZonedDateTimeFromUtc(Enrollment.FromDate, timeZoneId);
                Enrollment.ToDate = DateTimeHelper.GetZonedDateTimeFromUtc(Enrollment.ToDate, timeZoneId);
            }

            TimeZoneId = timeZoneId;
        }

        public override ClosestClassForBannerViewModel GetClosestCohealerClassForBanner(string coachTimeZoneId)
        {
            if (Status == ContributionStatuses.Completed.ToString())
            {
                return null;
            }

            var futureSessionTimes = Sessions.SelectMany(s => s.SessionTimes)
                .Where(st => !st.IsCompleted && st.EndTime >= DateTime.UtcNow)
                .ToList();

            if (futureSessionTimes.Count > 0)
            {
                var closestCohealerSessionTime = futureSessionTimes.OrderBy(st => st.StartTime).First();

                var minutesLeft = (int)closestCohealerSessionTime.StartTime.Subtract(DateTime.UtcNow).TotalMinutes;

                if (minutesLeft <= Constants.Contribution.Dashboard.MinutesMaxToShowCohealerClosestSessionBanner)
                {
                    var session = Sessions.FirstOrDefault(s => s.SessionTimes.Contains(closestCohealerSessionTime));

                    return new ClosestClassForBannerViewModel
                    {
                        ContributionId = Id,
                        ContributionTitle = Title,
                        ContributionType = Type,
                        ClassId = closestCohealerSessionTime.Id,
                        ClassGroupId = session?.Id,
                        Title = session?.Title,
                        MinutesLeft = minutesLeft < 0 ? 0 : minutesLeft,
                        ChatSid = Chat?.Sid,
                        IsRunning = closestCohealerSessionTime.VideoRoomInfo?.IsRunning ?? false,
                        StartTime = closestCohealerSessionTime.StartTime,
                        ContributionLiveVideoServiceProvider = LiveVideoServiceProvider,
                        ZoomStartUrl = closestCohealerSessionTime?.ZoomMeetingData?.StartUrl,
                        IsPrerecorded = session?.IsPrerecorded,
                        SessionTimes = session.SessionTimes,
                        PercentageCompleted = PercentageCompleted
                    };
                }
            }

            return null;
        }

        public override ClosestClassForBannerViewModel GetClosestClientClassForBanner(
            string clientId,
            string coachTimeZoneId)
        {
            if (Status == ContributionStatuses.Completed.ToString())
            {
                return null;
            }

            var clientSessionTimes = Sessions
                .SelectMany(s => s.SessionTimes)
                .Where(st => Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds == null
                    ? st.ParticipantsIds.Contains(clientId)
                    : st.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds).Contains(clientId))
                .ToList();

            if (!clientSessionTimes.Any())
            {
                return null;
            }

            var futureSessionTimes = clientSessionTimes
                .Where(st => !st.IsCompleted && st.EndTime >= DateTime.UtcNow)
                .ToList();

            if (futureSessionTimes.Count > 0)
            {
                var closestClientSessionTime = futureSessionTimes.OrderBy(st => st.StartTime).First();

                var minutesLeft = (int)closestClientSessionTime.StartTime.Subtract(DateTime.UtcNow).TotalMinutes;

                if (minutesLeft <= Constants.Contribution.Dashboard.MinutesMaxToShowClientClosestSessionBanner)
                {
                    var session = Sessions.FirstOrDefault(s => s.SessionTimes.Contains(closestClientSessionTime));

                    return new ClosestClassForBannerViewModel
                    {
                        AuthorUserId = UserId,
                        ContributionId = Id,
                        ContributionTitle = Title,
                        ContributionType = Type,
                        ClassId = closestClientSessionTime.Id,
                        ClassGroupId = session?.Id,
                        Title = session?.Title,
                        MinutesLeft = minutesLeft < 0 ? 0 : minutesLeft,
                        ChatSid = Chat?.Sid,
                        IsRunning = closestClientSessionTime.VideoRoomInfo?.IsRunning ?? false,
                        ContributionLiveVideoServiceProvider = LiveVideoServiceProvider,
                        PercentageCompleted = PercentageCompleted 
                        
                    };
                }
            }

            return null;
        }

        public override void AssignIdsToTimeRanges()
        {
            Sessions.ForEach(session =>
            {
                if (string.IsNullOrEmpty(session.Id))
                {
                    session.Id = Guid.NewGuid().ToString();
                }

                session.SessionTimes.ForEach(st =>
                {
                    if (string.IsNullOrEmpty(st.Id))
                    {
                        st.Id = Guid.NewGuid().ToString();
                    }
                });
            });
        }

        public OperationResult AssignUserToContributionTime(BookTimeBaseViewModel bookModel, UserViewModel user)
        {
            if (!(bookModel is BookSessionTimeViewModel bookSessionTimeModel))
            {
                return OperationResult.Failure("Unable to book slot in session time. Wrong book model type provided");
            }

            var session = Sessions.FirstOrDefault(s => s.Id == bookSessionTimeModel.SessionId);

            if (session is null)
            {
                return OperationResult.Failure(
                    $"Unable to assign user to session time. Session with Id {bookSessionTimeModel.SessionId} not found");
            }

            var sessionTime = session.SessionTimes.FirstOrDefault(st => st.Id == bookSessionTimeModel.SessionTimeId);

            if (sessionTime is null)
            {
                return OperationResult.Failure(
                    $"Unable to assign user to session time. Session time with Id {bookSessionTimeModel.SessionTimeId} not found");
            }

            if (session.SessionTimes.SelectMany(st => st.ParticipantsIds).Concat(Pods.SelectMany(x => x.ClientIds)).Contains(user.Id))
            {
                return OperationResult.Failure(
                    "User has already booked time in current session. Max 1 session time allowed to book");
            }

            var sessionTimeParticipants = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                ? sessionTime.ParticipantsIds
                : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).ToList();

            if (sessionTimeParticipants.Contains(user.Id))
            {
                return OperationResult.Failure("User has already booked time in current session time");
            }

            if (sessionTimeParticipants.Count >= session.MaxParticipantsNumber)
            {
                return OperationResult.Failure(
                    "User was not assigned to session time. Max participants number reached. Try another session time");
            }

            sessionTime.ParticipantsIds.Add(user.Id);

            return OperationResult.Success(string.Empty, bookSessionTimeModel);
        }

        public override OperationResult RevokeAssignmentUserToContributionTime(BookTimeBaseViewModel bookModel, UserViewModel user)
        {
            if (!(bookModel is BookSessionTimeViewModel bookSessionTimeModel))
            {
                return OperationResult.Failure("Unable revoke booking of session time. Wrong book model type provided");
            }

            var session = Sessions.FirstOrDefault(s => s.Id == bookSessionTimeModel.SessionId);

            if (session == null)
            {
                return OperationResult.Failure(
                    $"Unable revoke booking of session time. Session with Id {bookSessionTimeModel.SessionId} not found");
            }

            var sessionTime = session.SessionTimes.FirstOrDefault(st => st.Id == bookSessionTimeModel.SessionTimeId);

            if (sessionTime == null)
            {
                return OperationResult.Failure(
                    $". Session time with Id {bookSessionTimeModel.SessionTimeId} not found");
            }
            var sessionTimeParticipants = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                ? sessionTime.ParticipantsIds
                : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).ToList();

            if (!sessionTimeParticipants.Contains(user.Id))
            {
                return OperationResult.Failure(
                    $"Unable revoke booking of session time. User did not book session time with Id {bookSessionTimeModel.SessionTimeId}");
            }

            sessionTime.ParticipantsIds.Remove(user.Id);

            return OperationResult.Success();
        }

        public override void ClearHiddenForClientInfo(string clientUserId = null, PurchaseViewModel purchaseVm = null)
        {
            base.ClearHiddenForClientInfo(clientUserId);

            Sessions.ForEach(s => s.SessionTimes.ForEach(st =>
            {
                if (purchaseVm == null || !purchaseVm.HasAccessToContribution)
                {
                    st.RecordingInfos = new List<RecordingInfo>();
                }

                st.ParticipantsIds = HideParticipants(st.ParticipantsIds, clientUserId);
            }));
        }

        private List<string> HideParticipants(List<string> original, string clientUserId)
        {
            return original.Select(id =>
            {
                if (id == clientUserId)
                {
                    return id;
                }
                return "i";
            }).ToList();
        }

        public override void AssignChatSidForUserContributionPage(string clientUserId)
        {
        }

        private JourneyClassInfo MapContributionDetailsToJourneyClassInfo(
            Session session,
            SessionTime sessionTime,
            int numberCompletedClasses,
            string timeZoneId,
            string timeZoneShortName)
        {
            sessionTime.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, timeZoneId);
            sessionTime.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.EndTime, timeZoneId);
            return new JourneyClassInfo
            {
                ContributionId = Id,
                ClassId = sessionTime?.Id,
                AuthorUserId = UserId,
                PreviewContentUrls = PreviewContentUrls,
                Type = Type,
                ContributionTitle = session.Title,
                TotalNumberSessions = Sessions.Count,
                Rating = Rating,
                LikesNumber = LikesNumber,
                SessionTimeUtc = sessionTime?.StartTime,
                NumberCompletedSessions = numberCompletedClasses,
                SessionId = session.Id,
                IsPrerecorded = session.IsPrerecorded,
                PercentageCompleted = PercentageCompleted,
                SessionTitle = session.Name,
                IsCompleted = sessionTime.IsCompleted,
                SessionTimes = sessionTime,
                TimezoneId = timeZoneId,
                IsWorkshop = IsWorkshop,
                TimeZoneShortForm = timeZoneShortName,
            };
        }

        public override JourneyClassesInfosAll GetClassesInfosForParticipant(string participantId, string timeZoneId, string timeZoneShortName)
        {
            var numberCompletedClasses = Sessions.SelectMany(s => s.SessionTimes)
                .Count(st => (Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds == null
                    ? st.ParticipantsIds.Contains(participantId)
                    : st.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds).Contains(participantId)) && st.IsCompleted);

            var allClasses = new JourneyClassesInfosAll();

            if (IsCompleted && !IsAnyBooking(participantId))
            {
                //if user purchase, but not book any session.
                numberCompletedClasses = Sessions.Count;
                allClasses.OtherCompleted.AddRange(MapAllSessionsAsCompleted(numberCompletedClasses));
                return allClasses;
            }

            foreach (var session in Sessions)
            {
                foreach (var sessionTime in session.SessionTimes)
                {
                    if ((Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                        ? sessionTime.ParticipantsIds.Contains(participantId)
                        : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).Contains(participantId)
                        ) || (session.IsPrerecorded))
                    {
                        if (sessionTime.IsCompleted || sessionTime.CompletedSelfPacedParticipantIds.Contains(participantId))
                        {
                            allClasses.Past.Add(MapContributionDetailsToJourneyClassInfo(session, sessionTime,
                                numberCompletedClasses, timeZoneId, timeZoneShortName));
                        }
                        else
                        {
                            if (sessionTime.EndTime > DateTime.UtcNow)
                            {
                                allClasses.Upcoming.Add(MapContributionDetailsToJourneyClassInfo(session, sessionTime,
                                    numberCompletedClasses, timeZoneId, timeZoneShortName));
                            }
                            else
                            {
                                allClasses.OtherUncompleted.Add(
                                    MapContributionDetailsToJourneyClassInfo(session, sessionTime,
                                        numberCompletedClasses, timeZoneId, timeZoneShortName));
                            }
                        }
                    }
                    else if (!sessionTime.IsCompleted &&
                             !session.SessionTimes.Any(st => Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                             ? st.ParticipantsIds.Contains(participantId)
                             : st.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).Contains(participantId)))
                    {
                        allClasses.NotBooked.Add(
                            MapContributionDetailsToJourneyClassInfo(session, sessionTime, numberCompletedClasses,timeZoneId, timeZoneShortName));
                    }
                }
            }

            return allClasses;

            bool IsAnyBooking(string participantId)
            {
                return ClassesInfo.Values.Any(e => e.ParticipantIds.Contains(participantId));
            }

            IEnumerable<JourneyClassInfo> MapAllSessionsAsCompleted(int numberCompletedClasses)
            {
                return Sessions.Select(session =>
                    MapContributionDetailsToJourneyClassInfo(session, session.SessionTimes.FirstOrDefault(),
                        numberCompletedClasses,timeZoneId, timeZoneShortName));
            }
        }

        public override int GetTotalNumClassesForParticipant(string participantId)
        {
            return Sessions.Count;
        }

        public override List<ClosestCohealerSessionInfo> GetClosestCohealerSessions(bool fromDashboard = false)
        {
            var sessions = Sessions
                .Where(s => s.SessionTimes.Exists(!fromDashboard ? st => (true) : st => !st.IsCompleted))
                .Select(session => session.SessionTimes.Where(!fromDashboard ? st => (true) : st => !st.IsCompleted).Select(sessionTime =>
                    new ClosestCohealerSessionInfo
                    {
                        Title = session.Title,
                        Name = session.Name,
                        StartTime = sessionTime.StartTime,
                        ParticipantsIds = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                            ? sessionTime.ParticipantsIds
                            : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).ToList(),
                        EnrolledTotal = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                            ? sessionTime.ParticipantsIds.Count
                            : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).Count(),
                        EnrolledMax = session.MaxParticipantsNumber,
                        ClassId = sessionTime.Id,
                        ClassGroupId = session.Id,
                        ChatSid = Chat?.Sid,
                        ZoomStartMeeting = sessionTime?.ZoomMeetingData?.StartUrl,
                        IsPrerecorded = session.IsPrerecorded,
                        SessionTimes = session.SessionTimes.Where(st => !st.IsCompleted).ToList(),
                        IsCompleted = sessionTime.IsCompleted
                    }))
                .SelectMany(si => si)
                .OrderBy(si => si.StartTime)
                .ToList();

            return sessions;
        }
        public override List<ClosestCohealerSessionInfo> GetCohealerSessions()
        {
            var sessions = Sessions
                .Select(session => session.SessionTimes.Select(sessionTime =>
                    new ClosestCohealerSessionInfo
                    {
                        Title = session.Title,
                        Name = session.Name,
                        StartTime = sessionTime.StartTime,
                        ParticipantsIds = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                            ? sessionTime.ParticipantsIds
                            : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).ToList(),
                        EnrolledTotal = Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                            ? sessionTime.ParticipantsIds.Count
                            : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds).Count(),
                        EnrolledMax = session.MaxParticipantsNumber,
                        ClassId = sessionTime.Id,
                        ClassGroupId = session.Id,
                        ChatSid = Chat?.Sid,
                        ZoomStartMeeting = sessionTime?.ZoomMeetingData?.StartUrl,
                        IsPrerecorded = session.IsPrerecorded,
                        SessionTimes = session.SessionTimes.ToList(),
                        IsCompleted = sessionTime.IsCompleted
                    }))
                .SelectMany(si => si)
                .OrderBy(si => si.StartTime)
                .ToList();

            return sessions;
        }
        public SessionTime GetSessionTimeByClassId(string classId)
        {
            return Sessions.SelectMany(e => e.SessionTimes).FirstOrDefault(e => e.Id == classId);
        }

        public override List<SessionInfoForReminderViewModel> GetTomorrowSessions(
            DateTime tomorrowStartMomentUtc,
            DateTime dayAfterTomorrowStartMomentUtc)
        {
            var tomorrowSessionTimes = Sessions.Where(x=>!x.IsPrerecorded).SelectMany(s => s.SessionTimes).Where(st =>
                    !st.IsCompleted && st.StartTime >= tomorrowStartMomentUtc &&
                    st.StartTime <= dayAfterTomorrowStartMomentUtc)
                .ToList();

            List<SessionInfoForReminderViewModel> reminderModels = new List<SessionInfoForReminderViewModel>();

            if (tomorrowSessionTimes.Count > 0)
            {
                foreach (var sessionTime in tomorrowSessionTimes)
                {
                    foreach (var participantId in Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                        ? sessionTime.ParticipantsIds
                        : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds))
                    {
                        var reminderModel = new SessionInfoForReminderViewModel
                        {
                            AuthorUserId = UserId,
                            ClientUserId = participantId,
                            ClassId = sessionTime.Id,
                            ClassStartTimeUtc = sessionTime.StartTime,
                            ContributionId = Id,
                            ContributionTitle = Title
                        };

                        reminderModels.Add(reminderModel);
                    }
                }
            }

            return reminderModels;
        }

        public override OperationResult SetClassAsCompleted(string classId)
        {
            var sessionTime = Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == classId);

            if (sessionTime is null)
            {
                return OperationResult.Failure($"Unable to find session time with id: {classId}");
            }
            // Toggling IsCompleted Flag per request
            sessionTime.IsCompleted = !sessionTime.IsCompleted ;
            sessionTime.CompletedDateTime = DateTime.UtcNow;

            var session = Sessions.First(s => s.SessionTimes.Contains(sessionTime));
            if (session.SessionTimes.All(st => st.IsCompleted))
            {
                // Toggling IsCompleted Flag per request
                session.IsCompleted = !session.IsCompleted;
                session.CompletedDateTime = DateTime.UtcNow;
            }

            return OperationResult.Success(string.Empty, Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                ? sessionTime.ParticipantsIds
                : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds));
        }

        public override OperationResult SetSelfPacedClassAsCompleted(string classId, string clientId)
        {
            var sessionTime = Sessions.SelectMany(s => s.SessionTimes).FirstOrDefault(st => st.Id == classId);

            if (sessionTime is null)
            {
                return OperationResult.Failure($"Unable to find session time with id: {classId}");
            }

            if (!sessionTime.CompletedSelfPacedParticipantIds.Any(p => p == clientId))
            {
                sessionTime.CompletedSelfPacedParticipantIds.Add(clientId);
            }

            var session = Sessions.First(s => s.SessionTimes.Contains(sessionTime));

            return OperationResult.Success(string.Empty, Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds == null
                ? sessionTime.ParticipantsIds
                : sessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds));
        }

        public override void RevokeAllClassesBookedByUseId(string userId)
        {
            var bookedSessionTimes = Sessions.SelectMany(s => s.SessionTimes)
                .Where(st => Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds == null
                ? st.ParticipantsIds.Contains(userId)
                : st.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == st.PodId)?.ClientIds).Contains(userId));
            foreach (var bookedSessionTime in bookedSessionTimes)
            {
                bookedSessionTime.ParticipantsIds.Remove(userId);
            }
        }

        public override bool IsExistedSessionsModificationAllowed(
            ContributionBase existedContribution,
            out string errorMessage,
            out List<EditedBookingWithClientId> editedBookingsWithClientId,
            out List<DeletedBookingWithClientId> deletedBookingsWithClientId,
            out List<Document> deletedAttachments)
        {
            errorMessage = string.Empty;
            deletedBookingsWithClientId = new List<DeletedBookingWithClientId>();
            editedBookingsWithClientId = new List<EditedBookingWithClientId>();
            deletedAttachments = new List<Document>();

            if (!(existedContribution is SessionBasedContribution existedCourse))
            {
                errorMessage =
                    $"The existed contribution has type {existedContribution.Type} which is different from updated contribution type {Type}";
                return false;
            }

            var joinedSessions = existedCourse.Sessions.Join(Sessions, x => x.Id, y => y.Id,
                (x, y) => new { ExistingSession = x, IncomingSession = y }).ToList();

            if (Sessions.Count(x => x.Id != null) > joinedSessions.Count())
            {
                errorMessage =
                    "Some sessions in edited contributions that looks like existed have different Id from real existed sessions";
                return false;
            }

            var incomingSessionsWithNotNullIds = Sessions.Where(s => s.Id != null).ToList();

            if (existedCourse.Sessions.Count() > incomingSessionsWithNotNullIds.Count())
            {
                var existedSessionsIds = existedCourse.Sessions.Select(js => js.Id);
                var deletedSessionsIds = existedSessionsIds.Except(incomingSessionsWithNotNullIds.Select(s => s.Id));

                var deletedSessions = existedCourse.Sessions.Where(s => deletedSessionsIds.Contains(s.Id));

                foreach (var deletedSession in deletedSessions)
                {
                    foreach (var deletedSessionTime in deletedSession.SessionTimes)
                    {
                        foreach (var participantId in Pods.FirstOrDefault(x => x.Id == deletedSessionTime.PodId)?.ClientIds == null
                            ? deletedSessionTime.ParticipantsIds
                            : deletedSessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == deletedSessionTime.PodId)?.ClientIds))
                        {
                            deletedBookingsWithClientId.Add(new DeletedBookingWithClientId
                            {
                                ContributionId = Id,
                                ContributionTitle = Title,
                                ClassGroupId = deletedSession.Id,
                                ClassGroupName = deletedSession.Name,
                                ClassId = deletedSessionTime.Id,
                                ParticipantId = participantId,
                                DeletedStartTime = deletedSessionTime.StartTime,
                                ContributionName = Title
                            });
                        }
                    }

                    deletedAttachments.AddRange(deletedSession.Attachments);
                }
            }

            var isLiveViderServiceProviderChanged =
                existedContribution.LiveVideoServiceProvider != LiveVideoServiceProvider;

            foreach (var sPair in joinedSessions)
            {
                var joinedByIdSessionTimes = sPair.ExistingSession.SessionTimes.Join(sPair.IncomingSession.SessionTimes,
                    x => x.Id, y => y.Id,
                    (x, y) => new { ExistingSessionTime = x, IncomingSessionTime = y }).ToList();

                if (sPair.IncomingSession.SessionTimes.Count(x => x.Id != null) > joinedByIdSessionTimes.Count())
                {
                    errorMessage =
                        "Some incoming session times that looks like existed has different Id from real existed session times";
                    return false;
                }

                var incomingSessionTimesWithNotNullIds =
                    sPair.IncomingSession.SessionTimes.Where(st => st.Id != null).ToList();

                if (sPair.ExistingSession.SessionTimes.Count() > incomingSessionTimesWithNotNullIds.Count)
                {
                    var existedSessionTimesIds = sPair.ExistingSession.SessionTimes.Select(st => st.Id);
                    var deletedSessionTimesIds =
                        existedSessionTimesIds.Except(incomingSessionTimesWithNotNullIds.Select(st => st.Id));

                    var deletedSessionTimes =
                        sPair.ExistingSession.SessionTimes.Where(st => deletedSessionTimesIds.Contains(st.Id));

                    foreach (var deletedSessionTime in deletedSessionTimes)
                    {
                        foreach (var participantId in Pods.FirstOrDefault(x => x.Id == deletedSessionTime.PodId)?.ClientIds == null
                                                    ? deletedSessionTime.ParticipantsIds
                                                    : deletedSessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == deletedSessionTime.PodId)?.ClientIds))
                        {
                            deletedBookingsWithClientId.Add(new DeletedBookingWithClientId
                            {
                                ContributionId = Id,
                                ContributionTitle = Title,
                                ClassGroupId = sPair.ExistingSession.Id,
                                ClassGroupName = sPair.ExistingSession.Name,
                                ClassId = deletedSessionTime.Id,
                                ParticipantId = participantId,
                                DeletedStartTime = deletedSessionTime.StartTime,
                                ContributionName = Title
                            });
                        }
                    }
                }

                foreach (var stPair in joinedByIdSessionTimes)
                {
                    if (stPair.IncomingSessionTime.StartTime != stPair.ExistingSessionTime.StartTime)
                    {
                        foreach (var participantId in Pods.FirstOrDefault(x => x.Id == stPair.IncomingSessionTime.PodId)?.ClientIds == null
                            ? stPair.IncomingSessionTime.ParticipantsIds
                            : stPair.IncomingSessionTime.ParticipantsIds.Concat(Pods.FirstOrDefault(x => x.Id == stPair.IncomingSessionTime.PodId)?.ClientIds))
                        {
                            editedBookingsWithClientId.Add(new EditedBookingWithClientId
                            {
                                ContributionId = Id,
                                ContributionTitle = Title,
                                ClassGroupId = sPair.ExistingSession.Id,
                                ClassGroupName = sPair.ExistingSession.Name,
                                ClassId = stPair.IncomingSessionTime.Id,
                                ParticipantId = participantId,
                                NewStartTime = stPair.IncomingSessionTime.StartTime,
                                OldStartTime = stPair.ExistingSessionTime.StartTime,
                                ContributionName = Title,
                                UpdatedSessionName = sPair.IncomingSession.Name
                            });
                        }
                    }
                }
            }

            return true;
        }

        public override IEnumerable<string> GetAllIdentitiesInClass(string classId)
        {
            if (!ClassesInfo.ContainsKey(classId))
            {
                throw new ArgumentException("class not found");
            }

            var sessionTime = ClassesInfo[classId];

            var userIds = new List<string>() { UserId };

            userIds.AddRange(Partners.Select(e => e.UserId));
            userIds.AddRange(sessionTime.ParticipantIds);

            return userIds;
        }

        public override IEnumerable<CohealerContributionTimeRangeViewModel> GetCohealerContributionTimeRanges(Dictionary<string, string> clients, string contributorTimeZoneId, bool timesInUtc)
        {
            var today = DateTime.UtcNow.Date;

            var sessionTimes = Sessions
                                    .SelectMany(x => x.SessionTimes)
                                    .Where(c => c.StartTime >= today);

            return sessionTimes.Select(sessionTime => new CohealerContributionTimeRangeViewModel()
            {
                ContributionId = Id,
                ContributionType = Type,
                SessionName = Title,
                SessionStartTime = timesInUtc ? sessionTime.StartTime : DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, contributorTimeZoneId),
                SessionEndTime = timesInUtc ? sessionTime.EndTime : DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.EndTime, contributorTimeZoneId),
            });
        }
    }
}