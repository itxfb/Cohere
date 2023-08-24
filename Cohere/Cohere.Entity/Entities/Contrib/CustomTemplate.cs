using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities.Contrib
{
    public class CustomTemplate
    {
        public string Name { get; set; }
        public string EmailType { get; set; }
        public string EmailSubject { get; set; }
        public string EmailText { get; set; }
        public bool IsEmailEnabled { get; set; }
        public string Category { get; set; }
        public bool IsCustomBrandingColorsEnabled { get; set; } = false;
        public List<UniqueKeyWord> UniqueKeyWords { get; set; }
        public string ContributionId { get; set; }
    }
}
