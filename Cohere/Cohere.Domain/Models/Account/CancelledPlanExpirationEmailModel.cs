using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Cohere.Domain.Models.Account
{
    public class CancelledPlanExpirationEmailModel
    {
        public string customerName { get; set; }
        public string customerEmail { get; set; }
        public DateTime cancellationDate { get; set; }
        public DateTime expireDate { get; set; }
    }
}