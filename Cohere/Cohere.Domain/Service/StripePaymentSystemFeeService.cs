using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Options;
using System;

namespace Cohere.Domain.Service
{
    public class StripePaymentSystemFeeService : IPaymentSystemFeeService
    {
        private readonly IPricingCalculationService _pricingCalculationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStripeService _stripeService;

        private decimal StripeFixedFee { get; }

        private decimal StripePercentageFee { get; }

        public StripePaymentSystemFeeService(IOptions<PaymentFeeSettings> settings, IPricingCalculationService pricingCalculationService , IUnitOfWork unitOfWork , IStripeService stripeService)
        {
            StripeFixedFee = settings.Value.StripeFixedFee;
            StripePercentageFee = settings.Value.StripePercentageFee;
            _pricingCalculationService = pricingCalculationService;
            _unitOfWork = unitOfWork;
            _stripeService = stripeService;

        }

        /// <summary>
        /// Calculates the total amount to charge a customer in order to recoup all transaction fees.
        /// </summary>
        /// <param name="predictableAmount">Desired Net Amount (after fees)</param>
        /// <returns></returns>
        public decimal CalculateGrossAmount(decimal predictableAmount, bool coachPaysStripeFee, string coachId)
        {
            var chargeAmount = coachPaysStripeFee ? predictableAmount : (predictableAmount + StripeFixedFee) / (1 - StripePercentageFee);

            if (!String.IsNullOrEmpty(coachId))
            {

                var coachtUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == coachId);
                if (coachtUser.Result.ServiceAgreementType == "full")
                {
                    var country = _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == coachtUser.Result.CountryId);
                    try
                    {
                        var dynamicStripeFee = _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Result.Alpha2Code);
                        if(dynamicStripeFee.Result!=null)
                                chargeAmount = coachPaysStripeFee ? predictableAmount : (predictableAmount + dynamicStripeFee.Result.Fixed * _stripeService.SmallestCurrencyUnit) / (1 - (dynamicStripeFee.Result.Fee / _stripeService.SmallestCurrencyUnit));

                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                    }

                }

            }

            return _pricingCalculationService.TruncatePrice(chargeAmount);
        }

        /// <summary>
        /// Calculates the total amount to charge a customer in order to recoup all transaction fees.
        /// </summary>
        /// <param name="predictableAmount">Desired Net Amount (after fees)</param>
        /// <returns></returns>
        public long CalculateGrossAmountAsLong(decimal predictableAmount, bool coachPaysStripeFee ,string coachId)
        {
            return decimal.ToInt64(CalculateGrossAmount(predictableAmount, coachPaysStripeFee, coachId));
        }

        public decimal CalculateFee(decimal predictableAmount, bool coachPaysStripeFee, string coachId)
		{
            if (!String.IsNullOrEmpty(coachId))
            {

                var coachtUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == coachId);
                if (coachtUser.Result.ServiceAgreementType == "full")
                {
                    var country = _unitOfWork.GetRepositoryAsync<Country>().GetOne(e => e.Id == coachtUser.Result.CountryId);
                    var dynamicStripeFee = _unitOfWork.GetRepositoryAsync<StripeCountryFee>().GetOne(e => e.CountryCode == country.Result.Alpha2Code);
                    if (dynamicStripeFee.Result != null)
                        return coachPaysStripeFee ? 0 : (predictableAmount + dynamicStripeFee.Result.Fixed * _stripeService.SmallestCurrencyUnit) /( 1 - (dynamicStripeFee.Result.Fee / _stripeService.SmallestCurrencyUnit));

                }

            }
            return coachPaysStripeFee ? 0 : (predictableAmount * StripePercentageFee) + StripeFixedFee;
        }
    }
}
