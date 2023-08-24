namespace Cohere.Entity.Infrastructure.Options
{
    public class PaymentSettings
    {
        public double EscrowPeriodSeconds { get; set; }

        public decimal MaxCostAmountInCurrencyUnit { get; set; }
    }
}
