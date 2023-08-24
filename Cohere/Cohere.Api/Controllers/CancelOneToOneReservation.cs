using System;

namespace Cohere.Api.Controllers
{
    public class CancelOneToOneReservation
    {
        public string ContributionId { get; set; }

        public string BookedTimeId { get; set; }

        public DateTime Created { get; set; }
    }
}
