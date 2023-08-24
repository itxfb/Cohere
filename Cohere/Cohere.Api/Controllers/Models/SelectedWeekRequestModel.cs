using System;
using System.Collections.Generic;

namespace Cohere.Api.Controllers.Models
{
    public class SelectedWeekRequestModel
    {
        public List<SelectedDayRequestModel> Days { get; set; } = new List<SelectedDayRequestModel>();

        public DateTime EndTime { get; set; }

        public DateTime StartTime { get; set; }
    }
}
