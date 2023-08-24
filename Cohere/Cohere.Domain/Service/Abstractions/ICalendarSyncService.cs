using System.Collections.Generic;
using Cohere.Entity.Entities.Contrib;
using MimeKit;

namespace Cohere.Domain.Service.Abstractions
{
    public interface ICalendarSyncService
    {
        public AttachmentCollection CreateICalFile(
            string cohealerCommonName,
            string cohealerEmail,
            string organizerEmail,
            string locationUrl,
            EventDiff eventDiff,
            bool isCoach, string CustomInvitationBody);

        AttachmentCollection CreateICalFile(string recieverCommonName, string receiverEmail, string organizerEmail, string locationUrl, IEnumerable<BookedTimeToAvailabilityTime> createdOrUpdatedEvents, string CustomInvitationBody);
    }
}
