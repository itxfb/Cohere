using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.User
{
    public class BrandingColorsDTO
    {
        public string Id { get; set; }
        public Dictionary<string, string> BrandingColors { get; set; }
        public string CustomLogo { get; set; } = "";

    }
}
