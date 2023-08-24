using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasFreeBusyRequest : IEquatable<NylasFreeBusyRequest>
    {
        public string start_time { get; set; }

        public string end_time { get; set; }

        [NotNull]
        public IEnumerable<string> emails { get; set; }


        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(start_time);
            hashCode.Add(end_time);
            foreach (var item in emails.OrderBy(e => e))
            {
                hashCode.Add(item);
            }
            return hashCode.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NylasFreeBusyRequest);
        }

        public bool Equals([AllowNull] NylasFreeBusyRequest other)
        {
            if (other == null)
            {
                return false;
            }

            return start_time == other.start_time &&
                   end_time == other.end_time &&
                   emails.All(e => other.emails.Contains(e)) &&
                   emails.Count() == other.emails.Count();
        }
    }
}