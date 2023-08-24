using Microsoft.AspNetCore.Authorization;

namespace Cohere.Api.Controllers.Community
{
    using System;
    using System.Collections.Generic;
	using System.Threading.Tasks;
    using Cohere.Api.Models.Responses;
    using Cohere.Api.Utils;
	using Cohere.Api.Utils.Extensions;
	using Cohere.Domain.Models.ModelsAuxiliary;
	using Cohere.Domain.Service.Abstractions;
    using Cohere.Entity.Entities.Community;
	using Cohere.Entity.Enums.Contribution;
	using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("[controller]")]
    //[Authorize(Roles = "Cohealer")]
    [Authorize(Policy = "IsPaidTierPolicy")]
    public class CouponsController : CohereController
    {
        private readonly ICouponService _couponService;

        public CouponsController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        /// <summary>
        /// Create coupon
        /// </summary>
        /// <param name="request">Model as a <see cref="CreateCouponRequest"/>.</param>
        /// <response code="200">Creation Coupon successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns>Created Coupon.</returns>
        /// <example>POST: api/Coupon.</example>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Stripe.Coupon))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Create([FromBody] CreateCouponRequest request)
        {
            if (!Enum.TryParse<PaymentTypes>(request.PaymentType, out var paymentTypeEnum))
            {
                return BadRequest();
            }
            var createdCouponResult = await _couponService.CreateAsync(request, paymentTypeEnum);
            if (!createdCouponResult.Succeeded)
            {
                return BadRequest(new ErrorInfo(createdCouponResult.Message));
            }

            return Ok(createdCouponResult.Payload);
        }

        /// <summary>
        /// Update coupon
        /// </summary>
        /// <param name="request">Model as a <see cref="UpdateCouponRequest"/>.</param>
        /// <response code="200">Edition Coupon successfully done.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns>Updated Coupon.</returns>
        /// <example>PUT: api/Coupon.</example>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Stripe.Coupon))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Update([FromBody] UpdateCouponRequest request)
        {
            var updatedCouponResult = await _couponService.UpdateAsync(request);
            if (!updatedCouponResult.Succeeded)
            {
                return BadRequest(new ErrorInfo(updatedCouponResult.Message));
            }

            return Ok(updatedCouponResult.Payload);
        }

        /// <summary>
        /// Returns all coupons created by user
        /// </summary>
        /// <response code="200">Coupons returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns>Collection of all the coupon objects created by user.</returns>
        /// <example>GET: Coupon/GetAll.</example>
        [HttpGet("GetAll")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Stripe.Coupon))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetAll()
        {
            var userCoupons = await _couponService.GetAllCouponsAsync(AccountId);
            return Ok(userCoupons);
        }

        /// <summary>
        /// Returns the coupon by requested id.
        /// </summary>
        /// <param name="couponId">Coupon id.</param>
        /// <response code="200">Coupon returned successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns>Requested coupon is returned.</returns>
        /// <example>GET: Coupon/GetById.</example>
        [HttpGet("{couponId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Stripe.Coupon))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> GetById(string couponId)
        {
            var userCoupons = await _couponService.GetCouponAsync(couponId);
            return Ok(userCoupons);
        }

        /// <summary>
        /// Removes coupon by Id.
        /// </summary>
        /// <param name="couponId">Coupon id.</param>
        /// <response code="200">Coupon removed successfully.</response>
        /// <response code="400">Bad request.</response>
        /// <response code="500">Internal Server Error.</response>
        /// <returns>Deleted Coupon.</returns>
        /// <example>DELETE: api/Coupon/{couponId}.</example>
        [HttpDelete("{couponId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(FailureResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(FailureResponse))]
        public async Task<IActionResult> Delete([FromRoute] string couponId)
        {
            var deletedCoupon = await _couponService.DeleteAsync(couponId);
            return Ok(deletedCoupon);
        }

        //[Authorize]
        [AllowAnonymous]
        [HttpGet("ValidateByName/{couponName}/{contributionId}/{paymentOption}")]
        public async Task<IActionResult> ValidateByName([FromRoute] string couponName, [FromRoute]string contributionId, [FromRoute] PaymentOptions paymentOption)
		{
            var result = await _couponService.ValidateByNameAsync(couponName, contributionId, paymentOption);
            return Ok(result);

        }
    }
}