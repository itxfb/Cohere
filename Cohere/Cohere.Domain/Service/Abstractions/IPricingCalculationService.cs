using Cohere.Entity.Enums.Contribution;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IPricingCalculationService
    {
        decimal CalculatePlatformIncome(decimal amount, decimal platformPercentageFee);

        decimal CalculateBillingPlanCost(decimal amount, int splitNumbers);

        long CalculateServiceProviderIncomeAsLong(decimal amount, bool coachPaysStripeFee, decimal platformPercentageFee, PaymentTypes paymentType, string countryId, decimal? totalFees = null);

        decimal TruncatePrice(decimal price);

        decimal CalculateServiceProviderIncomeFromNetPurchaseAmount(decimal netAmount, decimal platformPercentageFee, bool coachPaysStripeFee, decimal grossAmount);

        ServiceProviderIncomeBreakdown CalculateServiceProviderIncome(decimal amount, bool coachPaysStripeFee, decimal platformPercentageFee, PaymentTypes paymentType, string countryId, decimal? totalFees = null);

        long CalculateServiceProviderIncomeFromNetPurchaseAmountAsLong(decimal netAmount, decimal platformPercentageFee, bool coachPaysStripeFee, decimal grossAmount);
    }
}