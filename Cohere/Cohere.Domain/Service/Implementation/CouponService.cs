namespace Cohere.Domain.Service.Implementation
{
    using System;
    using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
    using AutoMapper;
    using Cohere.Domain.Infrastructure;
	using Cohere.Domain.Infrastructure.Generic;
	using Cohere.Domain.Models;
	using Cohere.Domain.Models.Payment;
	using Cohere.Domain.Service.Abstractions;
    using Cohere.Entity.Entities;
    using Cohere.Entity.Entities.Community;
	using Cohere.Entity.Entities.Contrib;
	using Cohere.Entity.Enums.Contribution;
	using Cohere.Entity.UnitOfWork;
    using Stripe;
    using Coupon = Cohere.Entity.Entities.Coupon;

    public class CouponService : ICouponService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStripeService _stripeService;

        public CouponService(IMapper mapper, IUnitOfWork unitOfWork, IStripeService stripeService)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _stripeService = stripeService;
        }

        public async Task<OperationResult<CouponDto>> CreateAsync(CreateCouponRequest coupon, PaymentTypes paymentTypes)
        {
            coupon.Name = coupon?.Name?.Trim();
            var allCoupons = await _unitOfWork.GetRepositoryAsync<Coupon>().Get(c => c.CoachId == coupon.CoachId);
            var coach = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(m => m.Id == coupon.CoachId);
            if (allCoupons?.Any(c => c.Name == coupon.Name) == true)
			{
                return OperationResult<CouponDto>.Failure("Coupon with the same name already exists!");
            }
            var couponId = Guid.NewGuid().ToString();

            var options = _mapper.Map<CouponCreateOptions>(coupon);
            options.Id = couponId;
            // for now, hard code currency to usd
            options.Currency = "usd";
            if (!string.IsNullOrWhiteSpace(coupon.SelectedCurrency))
                options.Currency = coupon.SelectedCurrency.ToLower();
                

            var service = new Stripe.CouponService();

            var couponDto = _mapper.Map<Coupon>(coupon);
            couponDto.Id = couponId;
            couponDto.AllowedContributionTypes = coupon.AllowedContributionTypes;
            couponDto.Currency = options.Currency;

            Stripe.Coupon stripeCoupon;

            try
            {
                stripeCoupon = await service.CreateAsync(options, paymentTypes == PaymentTypes.Advance ? _stripeService.GetStandardAccountRequestOption(coach?.StripeStandardAccountId) : null);
            }
            catch (StripeException e)
            {
                throw new StripeException(e.Message);
            }

            await _unitOfWork.GetRepositoryAsync<Coupon>().Insert(couponDto);

            return OperationResult<CouponDto>.Success(_mapper.Map<CouponDto>(stripeCoupon));
        }

        public async Task<OperationResult<CouponDto>> UpdateAsync(UpdateCouponRequest coupon)
        {
            coupon.Name = coupon?.Name?.Trim();
            if (coupon.Id == null)
            {
                throw new ValidationException("Id of the coupon should not be null!");
            }

            var options = new CouponUpdateOptions
            {
                Name = coupon.Name,
                Metadata = coupon.Metadata,
            };

            var couponDto = await _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(c => c.Id == coupon.Id);
            var coach = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(m => m.Id == couponDto.CoachId);
            var allCoupons = await _unitOfWork.GetRepositoryAsync<Coupon>().Get(c => c.CoachId == couponDto.CoachId);
            if (allCoupons?.Any(c => c.Id != coupon.Id && c.Name == coupon.Name) == true)
            {
                return OperationResult<CouponDto>.Failure("Coupon with the same name already exists!");
            }

            var service = new Stripe.CouponService();
            couponDto.Name = coupon.Name;
            couponDto.Metadata = coupon.Metadata;
            Stripe.Coupon stripeCoupon;
            try
            {
                stripeCoupon = await service.UpdateAsync(coupon.Id, options, couponDto.PaymentType == PaymentTypes.Advance.ToString() ? _stripeService.GetStandardAccountRequestOption(coach?.StripeStandardAccountId) : null);
            }
            catch (StripeException e)
            {
                throw new StripeException(e.Message);
            }

            await _unitOfWork.GetRepositoryAsync<Coupon>().Update(coupon.Id, couponDto);

            return OperationResult<CouponDto>.Success(_mapper.Map<CouponDto>(stripeCoupon));
        }

        public async Task<IEnumerable<CouponDto>> GetAllCouponsAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var coupons = await _unitOfWork.GetRepositoryAsync<Coupon>().Get(c => c.CoachId == user.Id);

            return _mapper.Map<IEnumerable<CouponDto>>(coupons);
        }

        public async Task<CouponDto> GetCouponAsync(string couponId)
        {
            var coupon = await _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(c => c.Id == couponId);

            return _mapper.Map<CouponDto>(coupon);
        }

        public async Task<CouponDto> DeleteAsync(string couponId)
        {
            var service = new Stripe.CouponService();
            var coupon = await _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(c => c.Id == couponId);
            var coach = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(m => m.Id == coupon.CoachId);

            await service.DeleteAsync(couponId, null, coupon.PaymentType == PaymentTypes.Advance.ToString() ? _stripeService.GetStandardAccountRequestOption(coach?.StripeStandardAccountId) : null);

            await _unitOfWork.GetRepositoryAsync<Coupon>().Delete(couponId);
            return _mapper.Map<CouponDto>(coupon);
        }

        public async Task<ValidatedCouponViewModel> ValidateByIdAsync(string couponId, string contributionId, PaymentOptions paymentOption)
		{
            var coupon = await _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(c => c.Id == couponId);
            return await ValidateAsync(coupon, contributionId, null, paymentOption);
        }

        public async Task<ValidatedCouponViewModel> ValidateByNameAsync(string couponName, string contributionId, PaymentOptions paymentOption)
		{
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
            var coupon = await _unitOfWork.GetRepositoryAsync<Coupon>().GetOne(c => c.Name == couponName && c.CoachId == contribution.UserId);
            return await ValidateAsync(coupon, contributionId, contribution, paymentOption);

        }

        private async Task<ValidatedCouponViewModel> ValidateAsync(Coupon coupon, string contributionId, ContributionBase contribution, PaymentOptions paymentOption)
        {
            if (coupon != null)
            {
                var coach = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(m => m.Id == coupon.CoachId);
                var service = new Stripe.CouponService();
                var stripeCoupon = await service.GetAsync(coupon.Id, null, coupon.PaymentType == PaymentTypes.Advance.ToString() ? _stripeService.GetStandardAccountRequestOption(coach?.StripeStandardAccountId) : null);
                if (stripeCoupon?.Valid == true)
                {
                    if (coupon?.Metadata != null)
                    {
                        if (coupon.Metadata.TryGetValue(contributionId, out string metadataContributionId))
                        {
                            if (contribution == null)
                            {
                                contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(c => c.Id == contributionId);
                            }

                            // make sure that coheler is on a paid tier program
                            var contributerClient = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
                            var allPurchasedPlans = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                                .Get(pt => pt.ClientId == contributerClient.Id)).ToList();
                            var currentPurchasePlan = allPurchasedPlans?.OrderByDescending(p => p.CreateTime)?.FirstOrDefault();
                            var periodEnds = currentPurchasePlan?.Payments?.OrderByDescending(p => p.DateTimeCharged)?.FirstOrDefault()?.PeriodEnds;

                            if(periodEnds == null || periodEnds < DateTime.UtcNow)
                            {
                                return null;
                            }

                            //OperationResult purchaseDetailsResult = OperationResult.Failure("");
                            //if (contribution is ContributionCourse)
                            //{
                            //    purchaseDetailsResult = await _contributionPurchaseService.GetCourseContributionPurchaseDetailsAsync(contributionId, paymentOption, null);
                            //}
                            //else if (contribution is ContributionMembership)
                            //{
                            //    purchaseDetailsResult = await _contributionPurchaseService.GetMembershipContributionPurchaseDetailsAsync(contributionId, paymentOption, null);
                            //}
                            //else if (contribution is ContributionOneToOne)
                            //{
                            //    purchaseDetailsResult = await _contributionPurchaseService.GetOneToOneContributionPurchaseDetailsAsync(contributionId, paymentOption, null);
                            //}
                            //if (purchaseDetailsResult.Failed)
                            //{

                            //    return null;
                            //}
                            //ContributionPaymentDetailsViewModel contributionPaymentDetails = (ContributionPaymentDetailsViewModel)purchaseDetailsResult.Payload;

                            //decimal discountToAdd = 0;
                            //if (contributionPaymentDetails.DueNow > 0 && coupon.PercentOff > 0)
                            //{
                            //    discountToAdd = contributionPaymentDetails.DueNow * (decimal)coupon.PercentOff / 100;
                            //}
                            else if (/*contribution?.PaymentInfo?.Cost > 0 && */coupon.AmountOff > 0)
                            {
                                if (paymentOption == PaymentOptions.SplitPayments)
                                {
                                    if (contribution?.PaymentInfo?.SplitNumbers > 0)
                                    {
                                        // todo: check if we want the amount discount should be split by num of payments and how to do it in stripe
                                        //discountToAdd = (decimal)coupon.AmountOff;// / (int)contribution.PaymentInfo.SplitNumbers;
                                    }
                                }
                                else
                                {
                                    //discountToAdd = (decimal)coupon.AmountOff;
                                }
                            }
                            //discount += (decimal)discountToAdd;
                            return new ValidatedCouponViewModel()
                            {
                                Id = coupon?.Id,
                                //Discount = discount,
                                Name = coupon?.Name,
                                PercentAmount = coupon.PercentOff,
                                DiscountAmount = coupon.AmountOff
                            };
                        }
                    }
                }
            }
            return null;
        }

        public class ValidatedCouponViewModel
		{
            public string Id { get; set; }
            //public decimal Discount { get; set; }
            public string Name { get; set; }
            public decimal? PercentAmount { get; set; }
            public long? DiscountAmount { get; set; }
		}
    }
}