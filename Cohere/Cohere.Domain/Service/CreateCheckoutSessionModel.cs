using System.Collections.Generic;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Utils;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using Newtonsoft.Json;

namespace Cohere.Domain.Service
{
    public class CreateCheckoutSessionModel
    {
        public decimal? CouponPerecent { get; set; }

        public string ClientFirstName { get; set; }
        public string ClientLastName { get; set; }
        public string ClientEmail { get; set; }
        public string CoachEmail { get; set; }
        public string ContributionTitle { set; get; }


        public decimal TotalChargedCost { get; set; }
        public decimal StripeFee { get; set; }
        public decimal FixedStripeAmount { get; set; }
        public decimal InternationalFee { get; set; }
        public decimal? ProductCost { get; set; }

        public BillingPlanInfo BillingInfo { set; get; }

        public bool CoachPaysStripeFee { set; get; } = true;

        public bool IsStandardAccount { get; set; }

        public PaymentTypes paymentType { get; set; }

        public TaxTypes TaxType { get; set; }

        public decimal? DiscountPercent { set; get; }
        public string ServiceAgreementType { get; set; }
        public string StripeCustomerId { get; set; }

        public string ClientId { get; set; }

        public string ConnectedStripeAccountId { get; set; }

        public string StripeStandardAccountId { get; set; }

        public string PriceId { get; set; }

        public string ContributionId { get; set; }

        public PaymentOptions? PaymentOption { get; set; }

        public int? SplitNumbers { get; set; }
        
        public Dictionary<string, IEnumerable<string>> AvailabilityTimeIdBookedTimeIdPairsKey { get; set; }

        public string AvailabilityTimeId { get; set; }


        public string PurchaseId { get; set; }
        
        public string CouponId { get; set; }

        public string Currency { get; set; }

        public BookOneToOneTimeViewModel BookOneToOneTimeViewModel { get; set; }

        public Dictionary<string, string> GetMetadata()
        {
            var result = new Dictionary<string, string>()
            {
                {Constants.Stripe.MetadataKeys.ContributionId, ContributionId}
            };

            if (!(AvailabilityTimeIdBookedTimeIdPairsKey is null))
            {
                result.Add(Constants.Contribution.Payment.AvailabilityTimeIdBookedTimeIdPairsKey, JsonConvert.SerializeObject(AvailabilityTimeIdBookedTimeIdPairsKey));
            }

            //TODO: old subscription probably not have metadata at all, check it before prod
            if (!(PurchaseId is null))
            {
                result.Add(Constants.Stripe.MetadataKeys.PurchaseId, PurchaseId);
            }

            if(!(PaymentOption is null))
			{
                result.Add(Constants.Stripe.MetadataKeys.PaymentOption, PaymentOption.ToString());
            }

            if (!(SplitNumbers is null))
            {
                result.Add(Constants.Stripe.MetadataKeys.SplitNumbers, SplitNumbers.ToString());
            }

            if (!(CouponId is null))
            {
                result.Add(Constants.Stripe.MetadataKeys.CouponId, CouponId.ToString());
            }

            if (!(AvailabilityTimeId is null))
            {
                result.Add(Constants.Stripe.MetadataKeys.AvailabilityTimeId, AvailabilityTimeId.ToString());
            }

            if (!(BookOneToOneTimeViewModel is null))
            {
                result.Add(Constants.Contribution.Payment.BookOneToOneTimeViewModel, JsonConvert.SerializeObject(BookOneToOneTimeViewModel));
            }
            return result;
        }
            
            
    }
}