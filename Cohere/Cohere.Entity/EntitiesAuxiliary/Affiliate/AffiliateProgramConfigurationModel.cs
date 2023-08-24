namespace Cohere.Entity.EntitiesAuxiliary.Affiliate
{
    public class AffiliateProgramConfigurationModel
    {
        public long AffiliateFee { get; set; }

        public long FromReferralPaidTierFee { get; set; }

        public decimal? MaxRevenuePerReferral { get; set; }

        public int? MaxPeriodPerReferal { get; set; }
    }
}
