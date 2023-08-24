using Cohere.Domain.Infrastructure;
using Stripe;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IInvoicePaymentFailedEventService
    {
        OperationResult HandleInvoiceFailedStripeEvent(Event @event);
    }
}