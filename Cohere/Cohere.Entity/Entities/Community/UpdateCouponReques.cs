using System.ComponentModel.DataAnnotations;

namespace Cohere.Entity.Entities.Community
{
    using System.Collections.Generic;

    public class UpdateCouponRequest
    {
        [Required]
        public string Id { get; set; }

        public string Name { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
    }
}