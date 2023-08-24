using System.ComponentModel;

namespace Cohere.Entity.Enums.Payments
{
    public enum PaymentStatus
    {
        [Description("requires_payment_method")]
        RequiresPaymentMethod = 0,

        [Description("requires_confirmation")]
        RequiresConfirmation = 1,

        [Description("requires_action")]
        RequiresAction = 2,

        [Description("processing")]
        Processing = 3,

        [Description("requires_capture")]
        RequiresCapture = 4,

        [Description("canceled")]
        Canceled = 5,

        [Description("succeeded")]
        Succeeded = 6,

        [Description("paid")]
        Paid = 7
    }
}
