using AutoMapper;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities.Contrib;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cohere.Domain.Service
{
    public class CalendarSyncService : ICalendarSyncService
    {
        private readonly IMapper _mapper;

        public CalendarSyncService(IMapper mapper)
        {
            _mapper = mapper;
        }

        public AttachmentCollection CreateICalFile(
            string cohealerCommonName,
            string cohealerEmail,
            string organizerEmail,
            string locationUrl,
            EventDiff eventDiff,
            bool isCoach,string CustomInvitationBody)
        {
            var canceledCalendarEvents = _mapper.Map<List<CalendarEvent>>(eventDiff.CanceledEvents);

            foreach (var canceledEvent in canceledCalendarEvents)
            {
                canceledEvent.Status = EventStatus.Cancelled;
            }

            var allEvents = eventDiff.CreatedEvents
                .Concat(eventDiff.UpdatedEvents)
                .Concat(eventDiff.NotModifiedEvents);

            var mappedEvents = new List<CalendarEvent>();

            foreach (var @event in allEvents)
            {
                var mappedEvent = _mapper.Map<CalendarEvent>(@event);
                if (@event.SessionTime.ZoomMeetingData != null)
                {
                    mappedEvent.Status = EventStatus.Confirmed;
                    mappedEvent.Location = isCoach ? @event.SessionTime.ZoomMeetingData.StartUrl : @event.SessionTime.ZoomMeetingData.JoinUrl;
                }
                else
                {
                    mappedEvent.Location = locationUrl;
                }
                mappedEvent.Description = CustomInvitationBody;
                mappedEvents.Add(mappedEvent);
            }

            return BuildAttachmentCollection(cohealerEmail, cohealerCommonName, organizerEmail, mappedEvents.Concat(canceledCalendarEvents));
        }

        private AttachmentCollection BuildAttachmentCollection(string attendeeEmail, string commonName, string organizerEmail, IEnumerable<CalendarEvent> events)
        {
            var attendees = new List<Attendee> {
                new Attendee()
                {
                    CommonName = commonName,
                    ParticipationStatus = ParticipationRole.RequiredParticipant,
                    Rsvp = false,
                    Value = new Uri($"mailto:{attendeeEmail}")
                }
            };

            var orgatnaizer = new Organizer
            {
                CommonName = "Cohere",
                Value = new Uri($"mailto:{organizerEmail}")
            };

            foreach (var @event in events)
            {
                @event.Attendees = attendees;
                @event.Organizer = orgatnaizer;
            }

            var calendar = new Calendar();
            calendar.Method = CalendarMethods.Request;
            calendar.Events.AddRange(events);

            var serializer = new CalendarSerializer();
            var serializedCalendar = serializer.SerializeToString(calendar);

            var bytesCalendar = Encoding.ASCII.GetBytes(serializedCalendar);

            var atCol = new AttachmentCollection();

            atCol.Add("CohereEvents.ics", bytesCalendar, ContentType.Parse("application/ics"));

            return atCol;
        }

        public AttachmentCollection CreateICalFile(string recieverCommonName, string receiverEmail, string organizerEmail, string locationUrl, IEnumerable<BookedTimeToAvailabilityTime> createdOrUpdatedEvents, string CustomInvitationBody)
        {
            var allEvents = _mapper.Map<List<CalendarEvent>>(createdOrUpdatedEvents);
            foreach (var @event in allEvents)
            {
                @event.Location = locationUrl;
                @event.Description = CustomInvitationBody;
            }

            return BuildAttachmentCollection(receiverEmail, recieverCommonName, organizerEmail, allEvents);
        }
    }
}
