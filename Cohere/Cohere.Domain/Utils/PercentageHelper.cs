namespace Cohere.Domain.Utils
{
    public static class PercentageHelper
    {
        public static decimal SubtractPercent(this decimal amount, int? percent)
        {
            if (percent != null)
            {
                return (decimal)(amount - (amount / 100 * percent));
            }

            return amount;
        }

        public static decimal AddPercent(this decimal amount, int? percent)
        {
            if (percent != null)
            {
                return (decimal)(amount + (amount / 100 * percent));
            }

            return amount;
        }
    }
}
