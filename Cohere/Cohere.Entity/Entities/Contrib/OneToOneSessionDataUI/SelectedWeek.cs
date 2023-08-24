using System;
using System.Collections.Generic;

namespace Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI
{
    public class SelectedWeek
    {
        public List<SelectedDay> Days { get; set; } = new List<SelectedDay>();

        public DateTime EndTime { get; set; }

        public DateTime StartTime { get; set; }
    }
}
