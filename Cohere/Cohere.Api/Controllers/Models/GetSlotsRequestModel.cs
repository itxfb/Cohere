using System;
using System.Collections.Generic;

namespace Cohere.Api.Controllers.Models
{
    public class GetSlotsRequestModel
    {
        public DateTime StartDay { get; set; }

        public DateTime EndDay { get; set; }

        public string Duration { get; set; }

        public int SessionDuration { get; set; }

        public List<SelectedWeekRequestModel> SelectedWeeks { get; set; } = new List<SelectedWeekRequestModel>();

        public int Offset { get; set; }
    }
}
