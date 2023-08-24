using Cohere.Entity.Entities.Contrib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class EmailTemplates : BaseEntity
    {
        public string ContributionId { get; set; }
        public bool IsBrandingColorsEnabled { get; set; }
        public string UserId { get; set; }
        public string ContributionName { get; set; }
        public bool IsUpdated { get; set; }
        public List<CustomTemplate> CustomTemplates { get; set; }
    }
}
