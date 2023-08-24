namespace Cohere.Entity.Entities.Community
{
    using System.Collections.Generic;

    public class CreateCouponRequest
    {
        /// <summary>
        /// The Id of the Coach who created a coupon
        /// </summary>
        public string CoachId { get; set; }

        /// <summary>
        /// One of 'forever', 'once', and 'repeating' values. Describes how long a customer who applies this coupon will get the discount.
        /// </summary>
        public string Duration { get; set; }

        /// <summary>
        /// Select the currency for token generation.
        /// </summary>
        public string SelectedCurrency { get; set; }

        /// <summary>
        /// If duration is repeating, the number of months the coupon applies. Null if coupon duration is forever or once.
        /// </summary>
        public long? DurationInMonths { get; set; }

        /// <summary>
        /// Maximum number of times this coupon can be redeemed, in total, across all customers, before it is no longer valid.
        /// </summary>
        public long? MaxRedemptions { get; set; }

        /// <summary>
        /// Set of key-value pairs that you can attach to an object. This can be useful for storing additional information about the object in a structured format.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// List of the contribution type names which are allowed to use discounts with.
        /// </summary>
        public IEnumerable<string> AllowedContributionTypes { get; set; }

        public string Name { get; set; }

        public decimal? PercentOff { get; set; }

        public long? AmountOff { get; set; }

        /// <summary>
        /// Date after which the coupon can no longer be redeemed.
        /// </summary>
        public string RedeemBy { get; set; }
        public string PaymentType { get; set; }
    }
}