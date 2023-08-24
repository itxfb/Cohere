using System.Collections.Generic;
using System.Linq;
using Cohere.Entity.Enums.Payments;

namespace Cohere.Domain.Extensions
{
    public static class PaymentStatusExtensions
    {
        private static readonly Dictionary<PaymentStatus, string> EnumNames = new Dictionary<PaymentStatus, string>
        {
            { PaymentStatus.RequiresPaymentMethod, "requires_payment_method" },
            { PaymentStatus.RequiresConfirmation, "requires_confirmation" },
            { PaymentStatus.RequiresAction, "requires_action" },
            { PaymentStatus.Processing, "processing" },
            { PaymentStatus.RequiresCapture, "requires_capture" },
            { PaymentStatus.Canceled, "canceled" },
            { PaymentStatus.Succeeded, "succeeded" },
            { PaymentStatus.Paid, "paid" }
        };

        private static readonly Dictionary<string, PaymentStatus> NameEnums = EnumNames.ToDictionary(x => x.Value, y => y.Key);

        public static string GetName(this PaymentStatus status)
        {
            return EnumNames[status];
        }

        public static PaymentStatus ToPaymentStatusEnum(this string status)
        {
            return NameEnums[status];
        }
    }
}
