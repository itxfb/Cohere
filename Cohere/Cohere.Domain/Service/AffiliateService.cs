using System.Collections.Generic;
using System.Threading.Tasks;

using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;

namespace Cohere.Domain.Service
{
    public class AffiliateService : IAffiliateService
    {
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPayoutService _payoutService;
        private readonly IAffiliateCommissionService _affiliateCommissionService;

        public AffiliateService(INotificationService notificationService, IUnitOfWork unitOfWork,
            IPayoutService payoutService, IAffiliateCommissionService affiliateCommissionService)
        {
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _payoutService = payoutService;
            _affiliateCommissionService = affiliateCommissionService;
        }

        public async Task<OperationResult> ShareReferralLink(IEnumerable<string> emailAddresses, string inviterAccountId)
        {
            await _notificationService.SendReferralLinkEmailMessage(emailAddresses, inviterAccountId, inviterAccountId);
            return OperationResult.Success("Invitation message(s) have been sent");
        }

        public async Task<OperationResult<string>> GetUserNameByInviteCode(string inviteCode)
        {
            var affiliateAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == inviteCode);

            if (affiliateAccount is null)
            {
                return OperationResult<string>.Success(message: "Not Found");
            }

            if (!affiliateAccount.Roles.Contains(Roles.Cohealer))
            {
                return OperationResult<string>.Failure("not allowed account");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == affiliateAccount.Id);

            return OperationResult<string>.Success($"{user.FirstName} {user.LastName}");
        }

        public async Task<OperationResult> ToggleEnrollmentStatus(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);

            account.UnenrolledAffiliate = !account.UnenrolledAffiliate;

            await _unitOfWork.GetRepositoryAsync<Account>().Update(account.Id, account);

            return OperationResult.Success();
        }

        public async Task<OperationResult> GetPayout(string accountId, decimal amount)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);

            var availableBalance = await _affiliateCommissionService.GetAffiliateRevenueSummaryAsync(account.Id);

            if (availableBalance.Failed)
            {
                return availableBalance;
            }

            if (availableBalance.Payload.AvailableToPayoutRevenue < amount)
            {
                return OperationResult.Failure("insufficient funds");
            }

            return await _payoutService.GetPaidAsync(account.Id, amount, _payoutService.Currency, true);
        }

        public async Task<OperationResult> GetFullPayout(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);

            var availableBalance = await _affiliateCommissionService.GetAffiliateRevenueSummaryAsync(account.Id);

            if (availableBalance.Failed)
            {
                return availableBalance;
            }

            return await _payoutService.GetPaidAsync(
                account.Id,
                availableBalance.Payload.AvailableToPayoutRevenue,
                _payoutService.Currency,
                true);
        }

        public async Task<bool> IsEnrolled(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == accountId);

            return !account.UnenrolledAffiliate;
        }
    }
}