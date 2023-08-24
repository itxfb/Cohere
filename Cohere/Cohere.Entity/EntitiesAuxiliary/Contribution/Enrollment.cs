using System;

namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class Enrollment
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public bool AnyTime { get; set; }
    }
}
