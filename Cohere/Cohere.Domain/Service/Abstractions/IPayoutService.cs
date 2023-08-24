using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.Affiliate;
using Stripe;

namespace Cohere.Domain.Service.Abstractions
{
    public interface IPayoutService
    {
        string Currency { get; }

        decimal SmallestCurrencyUnit { get; }

        Task<OperationResult> GetPaidAsync(string accountId, decimal amount, string currency, bool isStandardAccount, bool affiliateRevenuePayout = false);

        Task<OperationResult> GetPaidAsync(string accountId, bool isStandardAccount, bool affiliateRevenuePayout = true);

        //Task<OperationResult> CreateTransferAsync(string sourceTransaction, string accountId, long amount);
        Task<OperationResult> CreateTransferAsync(string sourceTransaction, string accountId, long amount, string Currency = "");

        Task<OperationResult<CreateMoneyTransferResult>> CreateGroupTransferAsync(
            string sourceTransaction,
            string accountId,
            long amount,
            string affiliateAccountId,
            long affiliateRevenue);

        Task<Transfer> GetTransferAsync(string sourceTransaction, string accountId);

        Task<OperationResult> GetAvailableBalanceAsync(string accountId);
    }
}