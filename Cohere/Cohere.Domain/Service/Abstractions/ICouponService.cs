
namespace Cohere.Domain.Service.Abstractions
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
	using Cohere.Domain.Infrastructure.Generic;
	using Cohere.Domain.Models;
    using Cohere.Entity.Entities.Community;
	using Cohere.Entity.Enums.Contribution;
	using static Cohere.Domain.Service.Implementation.CouponService;

	public interface ICouponService
    {
        Task<OperationResult<CouponDto>> CreateAsync(CreateCouponRequest coupon, PaymentTypes paymentTypes);

        Task<OperationResult<CouponDto>> UpdateAsync(UpdateCouponRequest coupon);

        Task<IEnumerable<CouponDto>> GetAllCouponsAsync(string accountId);

        Task<CouponDto> GetCouponAsync(string couponId);

        Task<CouponDto> DeleteAsync(string couponId);

        Task<ValidatedCouponViewModel> ValidateByNameAsync(string couponName, string contributionId, PaymentOptions paymentOption); 
        Task<ValidatedCouponViewModel> ValidateByIdAsync(string couponId, string contributionId, PaymentOptions paymentOption);
    }
}