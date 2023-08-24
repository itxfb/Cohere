using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class DestinationBalanceTransaction
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal Fee { get; set; }
        public decimal Net { get; set; }
        public string SourceId { get; set; }
    }
}
