using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using Stripe;
using static Cohere.Domain.Utils.Constants.Stripe;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service
{
    public class StripePayoutService : IPayoutService
    {
        public string Currency => "usd";//By UZair;

        public decimal SmallestCurrencyUnit => 100m;

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ICohealerIncomeService _cohealerIncomeService;
        private readonly PayoutService _payoutService;
        private readonly TransferService _transferService;
        private readonly BalanceService _balanceService;
        private readonly IAffiliateCommissionService _affiliateCommissionService;
        private readonly ILogger<StripePayoutService> _logger;

        public StripePayoutService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            ICohealerIncomeService cohealerIncomeService,
            PayoutService payoutService,
            TransferService transferService,
            BalanceService balanceService,
            IAffiliateCommissionService affiliateCommissionService,
            ILogger<StripePayoutService> logger)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _cohealerIncomeService = cohealerIncomeService;
            _payoutService = payoutService;
            _transferService = transferService;
            _balanceService = balanceService;
            _affiliateCommissionService = affiliateCommissionService;
            _logger = logger;
        }

        public async Task<OperationResult> GetPaidAsync(string accountId, decimal amount, string currency, bool isStandardAccount, bool affiliateRevenuePayout = false)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (!user.PayoutsEnabled)
            {
                return OperationResult.Failure("Unable to get paid. Add your bank account information");
            }

            var balanceResult = await GetAvailableBalanceAsync(accountId);

            if (!balanceResult.Succeeded)
            {
                return balanceResult;
            }

            var availableBalance = (AvailableBalanceViewModel)balanceResult.Payload;
            long amountInCurrency = 0;
            long availableAmount = 0;

            if (isStandardAccount)
            {
                amountInCurrency = decimal.ToInt64(amount * SmallestCurrencyUnit);
                availableAmount = decimal.ToInt64((decimal)(availableBalance.StandardAccountAmount * SmallestCurrencyUnit));
            }
            else
            {
                amountInCurrency = decimal.ToInt64(amount * SmallestCurrencyUnit);
                availableAmount = decimal.ToInt64(availableBalance.Amount * SmallestCurrencyUnit);
            }

            if (availableAmount <= amountInCurrency)
            {
                var errorMessage = new StringBuilder($"Unable to payout {amount} {Currency}. ");
                errorMessage.Append(availableAmount > 0
                    ? $"You have {availableBalance.Amount} {Currency} available to payout"
                    : "You don't have available to payout fund");

                return OperationResult.Failure(errorMessage.ToString());
            }

            return await PayoutAsync(accountId, amountInCurrency, currency, affiliateRevenuePayout, isStandardAccount);
        }

        public async Task<OperationResult> GetPaidAsync(string accountId, bool isStandardAccount, bool affiliateRevenuePayout = false)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            if (!user.PayoutsEnabled)
            {
                return OperationResult.Failure("Unable to get paid. Add your bank account information");
            }

            var balanceResult = await GetAvailableBalanceAsync(accountId);

            if (!balanceResult.Succeeded)
            {
                return balanceResult;
            }

            var availableBalance = (AvailableBalanceViewModel)balanceResult.Payload;
            long availableAmount = 0;
            string currency = string.Empty;

            if (isStandardAccount)
            {
                availableAmount = decimal.ToInt64((decimal)(availableBalance.StandardAccountAmount * SmallestCurrencyUnit));
                currency = availableBalance.StandardAccountCurrency;
            }
            else
            {
                availableAmount = decimal.ToInt64(availableBalance.Amount * SmallestCurrencyUnit);
                currency = availableBalance.Currency;
            }

             if (availableAmount <= 0)
             {
                return OperationResult.Failure("You don't have available to payout fund");
             }

            var operationResult = await PayoutAsync(accountId, availableAmount, currency, affiliateRevenuePayout, isStandardAccount);

            if (operationResult.Succeeded)
            {
                var updateInfo = await _unitOfWork.GetRepositoryAsync<ReferralsInfo>().Get(a => a.ReferralUserId == user.Id);
                updateInfo = updateInfo.Where(a => (DateTime.UtcNow - a.TransferTime).TotalDays >= 60);
                updateInfo.ToList().ForEach(f =>
                {
                    f.IsPaidOut = true;
                    f.PaidOutTime = DateTime.UtcNow;
                    var d = _unitOfWork.GetGenericRepositoryAsync<ReferralsInfo>().Update(f.Id, f);
                });
            }


            return operationResult;
        }

        public async Task<OperationResult<CreateMoneyTransferResult>> CreateGroupTransferAsync(string sourceTransaction, string accountId, long amount, string affiliateAccountId, long affiliateRevenue)
        {
            try
            {
                var transferGroupIdentificator = GetUniqueTransferGroupIdentificator();

                var coachTransfer = await _transferService.CreateAsync(new TransferCreateOptions()
                {
                    Amount = amount,
                    Currency = Currency,
                    SourceTransaction = sourceTransaction,
                    Destination = accountId,
                    TransferGroup = transferGroupIdentificator
                });

                var affiliateTransfer = await _transferService.CreateAsync(new TransferCreateOptions()
                {
                    Amount = affiliateRevenue,
                    Currency = Currency,
                    SourceTransaction = sourceTransaction,
                    Destination = affiliateAccountId,
                    TransferGroup = transferGroupIdentificator
                });

                return OperationResult<CreateMoneyTransferResult>.Success(new CreateMoneyTransferResult
                {
                    CoachTransfer = coachTransfer,
                    AffiliateTransfer = affiliateTransfer
                });
            }
            catch (StripeException e)
            {
                return OperationResult<CreateMoneyTransferResult>.Failure(e.Message);
            }
        }

        public async Task<OperationResult> CreateTransferAsync(string sourceTransaction, string accountId, long amount, string Curency = "usd")
        {
            try
            {
                string _currency = Currency;
                var coachtUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.ConnectedStripeAccountId == accountId);
                if (coachtUser == null)
                {
                    if (coachtUser.ServiceAgreementType == "full")
                    {
                        _currency = Curency;
                    }

                }
                var transfer = await _transferService.CreateAsync(new TransferCreateOptions()
                {
                    Amount = amount,
                    Currency = _currency, 
                    SourceTransaction = sourceTransaction,
                    Destination = accountId
                });

                return OperationResult.Success(null, transfer);
            }
            catch (StripeException e)
            {
                return OperationResult.Failure(e.Message);
            }
        }

        public async Task<Transfer> GetTransferAsync(string sourceTransaction, string accountId)
        {
            var relatedTransfers = await _transferService.ListAsync(new TransferListOptions()
            {
                Destination = accountId
            });

            return relatedTransfers.FirstOrDefault(t => t.SourceTransactionId == sourceTransaction);
        }

        public async Task<OperationResult> GetAvailableBalanceAsync(string coachAccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == coachAccountId);

            var connectedStripeAccountId = user.ConnectedStripeAccountId;
            var standardAccountId = user.StripeStandardAccountId;

            if (connectedStripeAccountId == null)
            {
                _logger.LogError($"coach with accountId {user.AccountId} has no Stripe Account");
                return OperationResult.Failure("Stripe connected account is not found");
            }

            if (standardAccountId == null)
            {
                _logger.LogError($"coach with accountId {user.AccountId} has no Stripe Standard Account");
            }

            var totalIncome = await _cohealerIncomeService.GetTotalIncomeAsync(coachAccountId);

            if (totalIncome == null)
            {
                return OperationResult.Failure("Your income was not found");
            }

            var stripeAccBalance = await GetAccountBalanceAsync(connectedStripeAccountId);
            var stripeStandardAccountBalance = await GetAccountBalanceAsync(standardAccountId);

            var currencyAvailBalance = stripeAccBalance.Available.FirstOrDefault(/*b => b.Currency == Currency*/);
            var currencyAvailBalanceStandardAccount = stripeStandardAccountBalance?.Available.FirstOrDefault();

            if (currencyAvailBalance == null)
            {
                return OperationResult.Failure($"Your account doesn't have available balance in '{Currency}' currency");
            }

            if (currencyAvailBalanceStandardAccount == null)
            {
                _logger.LogError($"Your account doesn't have available balance in '{Currency}' currency in standard Account.");
            }

            var affiliateRevenue = await _affiliateCommissionService.GetAffiliateRevenueSummaryAsync(user.AccountId);

            var escrowAmount = decimal.ToInt64(totalIncome.EscrowIncomeAmount * SmallestCurrencyUnit);
            var escrowAmountStandrdAccount = decimal.ToInt64(totalIncome.EscrowIncomeAmountWithTaxIncluded * SmallestCurrencyUnit);

            //var affiliatePaidOutRevenue = decimal.ToInt64((affiliateRevenue.Payload.PaidOutRevenue) * SmallestCurrencyUnit);
            var affiliateRevenueAmountForPayout = decimal.ToInt64((affiliateRevenue.Payload.TotalRevenue - affiliateRevenue.Payload.PaidOutRevenue) * SmallestCurrencyUnit);
            

            var availableToPayoutAmount = currencyAvailBalance.Amount - escrowAmount;
            var availableToPayoutAmountStandrdAccount = currencyAvailBalanceStandardAccount?.Amount - escrowAmountStandrdAccount;

            if (affiliateRevenueAmountForPayout > availableToPayoutAmount)
                affiliateRevenueAmountForPayout = 0;

            availableToPayoutAmount = availableToPayoutAmount < 0 ? 0 : availableToPayoutAmount;
            availableToPayoutAmountStandrdAccount = availableToPayoutAmountStandrdAccount < 0 ? 0 : availableToPayoutAmountStandrdAccount;

            var model = new AvailableBalanceViewModel(
                currencyAvailBalance.Currency,
                availableToPayoutAmount / SmallestCurrencyUnit,
                affiliateRevenueAmountForPayout < 0 ? 0 : affiliateRevenueAmountForPayout / SmallestCurrencyUnit,
                currencyAvailBalanceStandardAccount?.Currency,
                availableToPayoutAmountStandrdAccount / SmallestCurrencyUnit,
                0
                );
            return OperationResult.Success(null, model);
        }

        private async Task<OperationResult> PayoutAsync(string accountId, long amount, string currency, bool affiliateRevenuePayout, bool isStandardAccount)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);

            var options = GetPayoutCreateOptions(amount, currency, affiliateRevenuePayout);

            try
            {
                string stripeCustomOrStandardaccountId = string.Empty;
                if (isStandardAccount)
                {
                    stripeCustomOrStandardaccountId = user.StripeStandardAccountId;
                }
                else
                {
                    stripeCustomOrStandardaccountId = user.ConnectedStripeAccountId;
                }
                var payout = await _payoutService.CreateAsync(options, new RequestOptions() { StripeAccount = stripeCustomOrStandardaccountId });
               
                var bankAccount = payout.Destination as BankAccount;
                await _notificationService.SendTransferMoneyNotification(user.FirstName, account.Email, bankAccount?.Last4);
                return OperationResult.Success(null, payout.ToJson());
            }
            catch (StripeException e)
            {
                return OperationResult.Failure(e.Message);
            }

            PayoutCreateOptions GetPayoutCreateOptions(long amount, string currency, bool affiliaterRevenue)
            {
                var options = new PayoutCreateOptions
                {
                    Amount = amount,
                    Currency = currency ?? Currency,
                    Method = "standard",
                };

                options.AddExpand("destination");

                if (affiliaterRevenue)
                {
                    options.Metadata = new Dictionary<string, string>
                    {
                        { MetadataKeys.IsAffiliateRevenue, true.ToString() }
                    };
                }

                return options;
            }
        }

        private async Task<Balance> GetAccountBalanceAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return null;
            }

            try
            {
                return await _balanceService.GetAsync(new RequestOptions { StripeAccount = accountId });
            }
            catch (StripeException)
            {
                return null;
            }
        }

        private string GetUniqueTransferGroupIdentificator()
        {
            return $"Order{Guid.NewGuid().ToString()}";
        }
    }
}
