using System;

namespace Cohere.Domain.Models.ContributionViewModels.ForClient
{
    public class EditedBookingWithClientId : DeletedBookingWithClientId
    {
        public DateTime NewStartTime { get; set; }
        public DateTime OldStartTime { get; set; }
        public string UpdatedSessionName { get; set; }
    }
}
