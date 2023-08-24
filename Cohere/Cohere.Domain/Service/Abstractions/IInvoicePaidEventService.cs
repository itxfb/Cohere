using Cohere.Domain.Infrastructure;
using Stripe;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IInvoicePaidEventService
    {
        OperationResult HandleInvoicePaidEvent(Event @event, bool forStandardAccount);
    }
}