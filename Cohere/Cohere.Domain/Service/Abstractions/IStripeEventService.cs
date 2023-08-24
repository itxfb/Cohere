using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Entity.Entities.Payment;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IStripeEventService : IServiceAsync<StripeEventViewModel, StripeEvent>
    {
        Task<bool?> IsProcessedEventAsync(string stripeEventId);
    }
}
