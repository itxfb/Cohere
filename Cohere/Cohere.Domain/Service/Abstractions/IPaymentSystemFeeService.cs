namespace Cohere.Domain.Service.Abstractions
{
    public interface IPaymentSystemFeeService
    {
        decimal CalculateGrossAmount(decimal predictableAmount, bool coachPaysStripeFee, string coachId);

        long CalculateGrossAmountAsLong(decimal predictableAmount, bool coachPaysStripeFee , string coachId);

        decimal CalculateFee(decimal predictableAmount, bool coachPaysStripeFee, string coachId);
    }
}
