using System.Collections.Generic;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;

namespace Cohere.Domain.Extensions
{
    public static class AvailabilityTimesExtensions
    {
        public static void CleanClientInfo(this IEnumerable<AvailabilityTime> availabilityTimes, string clientUserId)
        {
            foreach (var availabilityTime in availabilityTimes)
            {
                foreach (var bookedTime in availabilityTime.BookedTimes)
                {
                    if (clientUserId != null && bookedTime.ParticipantId == clientUserId)
                    {
                        continue;
                    }

                    bookedTime.ParticipantId = string.Empty;
                    bookedTime.RecordingInfos = new List<RecordingInfo>();
                }
            }
        }
    }
}
