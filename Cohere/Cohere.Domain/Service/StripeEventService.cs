using AutoMapper;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Generic;
using Cohere.Entity.Entities.Payment;
using Cohere.Entity.UnitOfWork;
using System.Threading.Tasks;

namespace Cohere.Domain.Service
{
    public class StripeEventService : GenericServiceAsync<StripeEventViewModel, StripeEvent>, IStripeEventService
    {
        public StripeEventService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<bool?> IsProcessedEventAsync(string stripeEventId)
        {
            var stripeEvent = await _unitOfWork.GetRepositoryAsync<StripeEvent>().GetOne(x => x.StripeEventId == stripeEventId);
            return stripeEvent?.IsProcessed;
        }
    }
}
