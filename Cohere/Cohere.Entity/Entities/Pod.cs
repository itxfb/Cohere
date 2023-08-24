using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Entities
{
    public class Pod : BaseEntity
    {
        public string Name { get; set; }

        public string CoachId { get; set; }

        public string ContributionId { get; set; }

        public List<string> ClientIds { get; set; }
    }
}
