using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class CustomLinks
    {
        public int Id { get; set; }
        public string UniqueName { get; set; }
        public string Link { get; set; }
        public string ImagePath { get; set; }
        public bool IsVisible { get; set; }
        public string Index { get; set; }
    }
}
