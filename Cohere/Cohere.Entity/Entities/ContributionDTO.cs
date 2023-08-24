using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class ContributionDTO
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string ImageUrl { get; set; }
        public string Index { get; set; }
        public bool IsEnabled { get; set; }
        public string Description { get; set; }


    }
}
