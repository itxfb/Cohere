using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Cohere.Api.Controllers;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.User;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;


namespace Cohere.Api.Auth
{
    public class IsPaidTierAuthorizationHandler : AuthorizationHandler<IsPaidTierAuthorizationRequirement>
    {
        private readonly IUnitOfWork _unitOfWork;

        public IsPaidTierAuthorizationHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsPaidTierAuthorizationRequirement requirement)
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
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var allPurchasedPlans = (await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>()
                .Get(pt => pt.ClientId == client.Id)).ToList();

            var currentPurchasePlan = allPurchasedPlans?.OrderByDescending(p => p.CreateTime)?.FirstOrDefault();
            var lastPayment = currentPurchasePlan?.Payments?.OrderByDescending(p => p.DateTimeCharged)?.FirstOrDefault();
            var paymentOption = lastPayment.PaymentOption;

            var periodEnds = lastPayment.DateTimeCharged;
            if (paymentOption.Equals(PaidTierOptionPeriods.Annually))
                periodEnds = periodEnds.AddYears(1);
            if (paymentOption.Equals(PaidTierOptionPeriods.Monthly))
                periodEnds = periodEnds.AddMonths(1);
            if (paymentOption.Equals(PaidTierOptionPeriods.EverySixMonth))
                periodEnds = periodEnds.AddMonths(6);

            bool isAuthorized = periodEnds >= DateTime.UtcNow;

            if (isAuthorized)
            {
                context.Succeed(requirement);
            }
        }
    }
}
