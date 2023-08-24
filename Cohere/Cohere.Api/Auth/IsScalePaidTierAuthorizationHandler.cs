using Cohere.Domain.Models.Payment;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cohere.Api.Auth
{
    public class IsScalePaidTierAuthorizationHandler : AuthorizationHandler<IsScalePaidTierAuthorizationRequirement>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;

		public IsScalePaidTierAuthorizationHandler(
			IUnitOfWork unitOfWork, IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService, IMemoryCache memoryCache)
		{
			_unitOfWork = unitOfWork;
			_paidTiersService = paidTiersService;
			_memoryCache = memoryCache;
		}

		protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsScalePaidTierAuthorizationRequirement requirement)
        {
            if (context.User == null)
            {
                return;
            }

            var user = context.User;

            if (!user.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            {
                return;
            }

            if (user.IsInRole(Roles.Admin.ToString()) || user.IsInRole(Roles.SuperAdmin.ToString()))
            {
                context.Succeed(requirement);
                return;
            }

            var accountId = context.User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier).Value;
            var currentPaidTier = await _memoryCache.GetOrCreateAsync("currentPaidTier_" + accountId, async entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromDays(1));
                return await _paidTiersService.GetCurrentPaidTier(accountId);
            });

            if (currentPaidTier.PaidTierOption.DisplayName == Constants.PaidTierTitles.Scale)
            {
                context.Succeed(requirement);
            }
        }
    }
}
