using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.ContributionViewModels
{
    public class SignoffInfoViewModel
    {
        public string ContributionId { get; set; }
        public string IPAddress { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
