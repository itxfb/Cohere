using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Domain.Models.Payment
{
    public class PurchaseCommunityContributionViewModel : PurchaseContributionViewModel
    {
        public string PaymentOption { get; set; }
    }
}
