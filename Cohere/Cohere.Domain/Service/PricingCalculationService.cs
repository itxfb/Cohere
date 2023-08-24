using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Options;

namespace Cohere.Domain.Service
{
    public class PricingCalculationService : IPricingCalculationService
    {
        private decimal StripeFixedFee { get; }

        private decimal StripePercentageFee { get; }

        private decimal StripeInternationalCardPercentageFee { get; }

        private readonly IUnitOfWork _unitOfWork;
        private readonly IStripeService _stripeService;

        public PricingCalculationService(IOptions<PaymentFeeSettings> settings, IUnitOfWork unitOfWork, IStripeService stripeService)
        {
            StripeFixedFee = settings.Value.StripeFixedFee;
            StripePercentageFee = settings.Value.StripePercentageFee;
            StripeInternationalCardPercentageFee = settings.Value.StripeInternationalCardPercentageFee;
            _unitOfWork = unitOfWork;
            _stripeService = stripeService;
        }

        public decimal CalculatePlatformIncome(decimal amount, decimal platformPercentageFee)
        {
            return TruncatePrice(amount * platformPercentageFee);
        }

        public decimal TruncatePrice(decimal price)
        {
            return decimal.Truncate(decimal.Round(price - decimal.Truncate(price)) > 0 ? price + 1 : price);
        }

        public ServiceProviderIncomeBreakdown CalculateServiceProviderIncome(
            decimal amount,
            bool coachPaysStripeFee,
            decimal platformPercentageFee,
            PaymentTypes paymentType,
            string countryId,
            decimal? totalFees = null)
        {

            var stripeFee = 0m;
            if (coachPaysStripeFee)
            {
                stripeFee = (amount * StripePercentageFee) + StripeFixedFee;

                if (!string.IsNullOrEmpty(countryId))
                {
                    var coachCountry = _unitOfWork.GetGenericRepositoryAsync<Country>().GetOne(c => c.Id == countryId).GetAwaiter().GetResult();
                    if (coachCountry != null)
                    {
                        var dynamicStripeFee = _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == coachCountry.Alpha2Code).GetAwaiter().GetResult();
                        if(dynamicStripeFee != null)
                        {
                            stripeFee = (amount * dynamicStripeFee.Fee / 100) + dynamicStripeFee.Fixed * _stripeService.SmallestCurrencyUnit;
                        }
                    }
                }
            }

            if (totalFees != null && paymentType == PaymentTypes.Advance && coachPaysStripeFee)
            {
                stripeFee = (decimal)totalFees;
            }

            var platformFee = (amount - (!coachPaysStripeFee && totalFees != null ? (decimal)totalFees : 0)) * platformPercentageFee;
            var extraFees = totalFees != null && totalFees > stripeFee ? ((decimal)totalFees - stripeFee) : 0m;
            return new ServiceProviderIncomeBreakdown()
            {
                Total = TruncatePrice(amount - platformFee - stripeFee - extraFees),
                PlatformFee = platformFee,
                StripeFee = stripeFee,
                ExtraFees = extraFees,
            };
        }

        public long CalculateServiceProviderIncomeAsLong(
            decimal amount,
            bool coachPaysStripeFee,
            decimal platformPercentageFee,
            PaymentTypes paymentType,
            string countryId,
            decimal? totalFees = null)
        {
            return decimal.ToInt64(CalculateServiceProviderIncome(amount, coachPaysStripeFee, platformPercentageFee, paymentType, countryId, totalFees).Total);
        }

        public decimal CalculateServiceProviderIncomeFromNetPurchaseAmount(decimal netAmount, decimal platformPercentageFee, bool coachPaysStripeFee, decimal grossAmount)
        {
            if (coachPaysStripeFee)
            {
                var cohereFee = grossAmount * platformPercentageFee;
                return TruncatePrice(netAmount - cohereFee);
            }

            return TruncatePrice(netAmount * (1m - platformPercentageFee));
        }

        public long CalculateServiceProviderIncomeFromNetPurchaseAmountAsLong(decimal netAmount, decimal platformPercentageFee, bool coachPaysStripeFee, decimal grossAmount)
        {
            return decimal.ToInt64(CalculateServiceProviderIncomeFromNetPurchaseAmount(netAmount, platformPercentageFee, coachPaysStripeFee, grossAmount));
        }

        public decimal CalculateBillingPlanCost(decimal amount, int splitNumbers)
        {
            var oneTimeAmount = amount / splitNumbers;
            return TruncatePrice(oneTimeAmount);
        }
    }

    public class ServiceProviderIncomeBreakdown
    {
        public decimal Total { get; set; }
        public decimal StripeFee { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal ExtraFees { get; set; }
    }
}