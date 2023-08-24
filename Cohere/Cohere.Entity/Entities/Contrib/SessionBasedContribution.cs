using System.Collections.Generic;
using System.Linq;

using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Entity.Entities.Contrib
{
    public abstract class SessionBasedContribution : ContributionBase
    {
        public bool IsLive { get; set; }

        public List<Session> Sessions { get; set; }

        public override List<string> RecordedRooms =>
            Sessions?.SelectMany(s => s.SessionTimes).SelectMany(st => st.RecordingInfos).Select(ri => ri.RoomId).ToList();

        public Enrollment Enrollment { get; set; }

        public Dictionary<string, SessionTimeToSession> GetSessionTimes(string clientName, bool withPreRecorded = true) => Sessions?
            .Where(s => withPreRecorded || !s.IsPrerecorded)
            ?.SelectMany(e => e.SessionTimes
                .Select(s => new SessionTimeToSession
                {
                    ClientName = clientName,
                    Session = e,
                    SessionTime = s,
                    ContributionName = Title,
                    CreatedDateTime = CreateTime
                }))
            .ToDictionary(key => key.SessionTime.Id)
            ?? new Dictionary<string, SessionTimeToSession>();

        public EventDiff
            GetEventsDiff(string clientName, SessionBasedContribution updatedCourse, bool withPreRecorded = false)
        {
            var updatedSessionTimesDict = updatedCourse.GetSessionTimes(clientName, withPreRecorded);
            var existedSessionTimesDict = GetSessionTimes(clientName, withPreRecorded);

            var canceledEvents = existedSessionTimesDict.Keys
                .Except(updatedSessionTimesDict.Keys)
                .Where(existedSessionTimesDict.ContainsKey)
                .Select(e => existedSessionTimesDict[e])
                .ToList();

            var createdEvents = updatedSessionTimesDict.Keys
                .Except(existedSessionTimesDict.Keys)
                .Where(updatedSessionTimesDict.ContainsKey)
                .Select(e => updatedSessionTimesDict[e])
                .ToList();

            var notModifiedEvents = updatedSessionTimesDict.Values
                .Intersect(existedSessionTimesDict.Values, SessionTimeToSession.SessionTimeToSessionEventEqualityComparer.Instance)
                .ToList();

            var updatedEvents = updatedSessionTimesDict.Values
                .Except(createdEvents)
                .Except(notModifiedEvents)
                .ToList();

            if (updatedCourse.LiveVideoServiceProvider != LiveVideoServiceProvider)
            {
                updatedEvents.AddRange(notModifiedEvents);
                notModifiedEvents = new List<SessionTimeToSession>();
            }

            return new EventDiff
            {
                CreatedEvents = createdEvents,
                UpdatedEvents = updatedEvents,
                CanceledEvents = canceledEvents,
                NotModifiedEvents = notModifiedEvents
            };
        }

        public Session GetSessionBySessionTimeId(string sessionTimeId)
        {
            foreach(var session in Sessions)
            {
                foreach(var sessiontime in session.SessionTimes)
                {
                    if (sessionTimeId == sessiontime.Id)
                    {
                        return session;
                    }
                }
            }
            return null;
        }

        public Session GetSessionByRoomId(string roomId)
        {
            foreach (var session in Sessions)
            {
                foreach (var sessiontime in session.SessionTimes)
                {
                    foreach (var recordingInfo in sessiontime.RecordingInfos)
                    {
                        if (roomId == recordingInfo.RoomId)
                        {
                            return session;
                        }
                    }

                }
            }
            return null;
        }

        public override void CleanSessions()
        {
            Sessions = new List<Session>();
        }

        public override bool IsCompletedTimesChanged(ContributionBase contributionToCheck, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (contributionToCheck.Type != Type)
            {
                errorMessage = $"The existed contribution has type {contributionToCheck.Type} which is different from updated contribution type {Type}";
                return true;
            }

            var sessionBasedContributionToCheck = contributionToCheck as SessionBasedContribution;
            var existedSessionTimes = GetCompletedSessionTimes(Sessions);
            var sessionTimesToCheck = GetCompletedSessionTimes(sessionBasedContributionToCheck?.Sessions);
            SynchronizeWithExistedData(existedSessionTimes, sessionTimesToCheck);

            var timesEqual = existedSessionTimes.Count == sessionTimesToCheck.Count && existedSessionTimes.All(n => sessionTimesToCheck.Contains(n));

            if (!timesEqual)
            {
                errorMessage = "You try to delete completed sessions";
                return true;
            }

            return false;
        }

        private List<SessionTime> GetCompletedSessionTimes(List<Session> sessions)
        {
            var completedSessionTimes = new List<SessionTime>();
            foreach (var time in sessions)
            {
                completedSessionTimes.AddRange(time.SessionTimes.FindAll(t => t.IsCompleted || t.RecordingInfos.Count != 0));
            }

            return completedSessionTimes;
        }

        private void SynchronizeWithExistedData(List<SessionTime> existedList, List<SessionTime> listToSynchronize)
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
    }
}
