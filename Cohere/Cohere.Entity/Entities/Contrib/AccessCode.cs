using System;

namespace Cohere.Entity.Entities.Contrib
{
    public class AccessCode : BaseEntity
    {
        public string Code { get; set; }
        public string ContributionId { get; set; }
        public string CreatorId { get; set; }
        public DateTime ValidTill { get; set; }
    }
}