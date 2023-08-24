using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Enums.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Cohere.Entity.Entities.Invoice
{
    public class StripeInvoice : BaseEntity
    {
        public string ClientId { get; set; }
        public string InvoiceId { get; set; }
        public string ContributionId { get; set; }
        public string PaymentOption { get; set; }
        public bool IsCancelled { get; set; } = false;
    }
}