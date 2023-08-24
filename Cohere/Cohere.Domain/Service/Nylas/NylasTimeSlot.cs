using System;
using Cohere.Entity.EntitiesAuxiliary.Contribution;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasTimeSlot
    {
        public string @object { get; set; }

        public string status { get; set; }

        public double start_time { get; set; }

        public double end_time { get; set; }

        public TimeRange ToTimeRange()
        {
            return new TimeRange()
            {
                StartTime = UnixTimeStampToDateTime(start_time),
                EndTime = UnixTimeStampToDateTime(end_time)
            };
        }

        private DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime;
        }
    }
}