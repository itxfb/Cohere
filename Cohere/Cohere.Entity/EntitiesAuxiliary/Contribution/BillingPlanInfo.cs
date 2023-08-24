namespace Cohere.Entity.EntitiesAuxiliary.Contribution
{
    public class BillingPlanInfo
    {
        /// <summary>
        /// Strip's product Id
        /// </summary>
        public string ProductBillingPlanId { get; set; }

        /// <summary>
        /// Total amount to charge a customer per interval
        /// </summary>
        public decimal BillingPlanGrossCost { get; set; }

        /// <summary>
        /// Price per interval
        /// </summary>
        public decimal BillingPlanPureCost { get; set; }

        /// <summary>
        /// Service provider income per interval
        /// </summary>
        public decimal BillingPlanTransferCost { get; set; }

        /// <summary>
        /// Total amount of charges (charge per interval * split number)
        /// </summary>
        public decimal TotalBillingGrossCost { get; set; }

        /// <summary>
        /// Total Price of product (price per interval * split number)
        /// </summary>
        public decimal TotalBillingPureCost { get; set; }
    }
}
