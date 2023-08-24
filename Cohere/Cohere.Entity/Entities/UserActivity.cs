using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Entities
{
    public class UserActivity : BaseEntity
    {
        public string UserId { get; set; }
        public DateTime ActivityTimeUTC { get; set; }
    }
}
