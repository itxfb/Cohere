namespace Cohere.Domain.Models.Affiliate
{
    public class AffiliateRevenueModel : AffiliateRevenueModelBase
    {
        public decimal PaidOutRevenue { get; set; }

        public decimal AvailableToPayoutRevenue { get; set; }

        public long ReferralsCount { get; set; }

        public long ReferralsWithSalesCount { get; set; }
    }
}
