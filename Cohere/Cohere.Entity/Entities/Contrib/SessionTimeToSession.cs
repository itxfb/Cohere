using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Entity.Entities.Contrib
{
    public class SessionTimeToSession
    {
        public Session Session { get; set; }

        public string ClientName { get; set; }

        public string ContributionName { get; set; }

        public string TimeZoneName { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public SessionTime SessionTime { get; set; }

        /// <summary>
        /// Check if calendar event related properties are equal
        /// </summary>
        /// <seealso cref="SessionTime" />
        public class SessionTimeToSessionEventEqualityComparer : IEqualityComparer<SessionTimeToSession>
        {
            public static SessionTimeToSessionEventEqualityComparer Instance =>
                new SessionTimeToSessionEventEqualityComparer();

            public bool Equals([AllowNull] SessionTimeToSession x, [AllowNull] SessionTimeToSession y)
            {
                if (x is null || y is null)
                {
                    return false;
                }

                return x.SessionTime.Id == y.SessionTime.Id &&
                       x.SessionTime.StartTime == y.SessionTime.StartTime &&
                       x.SessionTime.EndTime == y.SessionTime.EndTime &&
                       x.Session.Id == y.Session.Id &&
                       x.Session.Title == y.Session.Title &&
                       x.Session.Name == y.Session.Name;
            }

            public int GetHashCode([DisallowNull] SessionTimeToSession obj)
            {
                return HashCode.Combine(
                    obj.SessionTime.Id,
                    obj.SessionTime.StartTime,
                    obj.SessionTime.EndTime,
                    obj.Session.Id,
                    obj.Session.Title,
                    obj.Session.Name);
            }
        }
    }
}