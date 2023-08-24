using System;
using System.Collections.Generic;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class SessionReminderViewModel : IEqualityComparer<SessionReminderViewModel>
    {
        public string AuthorUserId { get; set; }

        public string AuthorFirstName { get; set; }

        public string AuthorEmail { get; set; }

        public string AuthorTimeZoneId { get; set; }

        public DateTime AuthorClassStartTimeZoned { get; set; }

        public bool IsAuthorEmailNotificationsEnabled { get; set; }

        public string ClientUserId { get; set; }

        public string ClientFirstName { get; set; }

        public string ClientEmail { get; set; }

        public string ClientTimeZoneId { get; set; }

        public DateTime ClientClassStartTimeZoned { get; set; }

        public bool IsClientEmailNotificationsEnabled { get; set; }

        public string ContributionId { get; set; }

        public string ContributionTitle { get; set; }

        public string ClassId { get; set; }

        public bool Equals(SessionReminderViewModel x, SessionReminderViewModel y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return x.AuthorUserId == y.AuthorUserId && x.ContributionId == y.ContributionId && x.ClassId == y.ClassId;
        }

        public int GetHashCode(SessionReminderViewModel obj)
        {
            return 1;
        }
    }
}
