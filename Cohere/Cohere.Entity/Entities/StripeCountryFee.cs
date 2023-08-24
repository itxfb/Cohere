using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class StripeCountryFee : BaseEntity
    {
       
        public string Country { get; set; }

        public decimal Fee { get; set; }

        public decimal Fixed { get; set; }

        public decimal International { get; set; }
        public string CountryCode { get; set; }
    }
}
