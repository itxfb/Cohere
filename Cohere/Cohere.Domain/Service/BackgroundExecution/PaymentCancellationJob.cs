using Cohere.Domain.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using Cohere.Entity.Enums.Payments;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class PaymentCancellationJob : IPaymentCancellationJob
    {
        private readonly IStripeService _stripeService;
        private readonly ILogger<PaymentCancellationJob> _logger;
        private readonly int _retryPolicyNumber;

        public const string RetryPolicyNumber = "PaymentCancellationJobRetryPolicy";

        public PaymentCancellationJob(IStripeService stripeService, Func<string, int> integersResolver, ILogger<PaymentCancellationJob> logger)
        {
            _stripeService = stripeService;
            _logger = logger;
            _retryPolicyNumber = integersResolver.Invoke(RetryPolicyNumber);
        }

        public void Execute(params object[] args)
        {
            var retryNumber = 0;
            OperationResult cancellationResult;
            do
            {
                if (retryNumber > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                }

                var paymentIntent = _stripeService.GetPaymentIntentAsync((string)args[0]).GetAwaiter().GetResult();
                var paymentStatus = paymentIntent.Status.ToPaymentStatusEnum();

                if (paymentStatus != PaymentStatus.Canceled && paymentStatus != PaymentStatus.Succeeded)
                {
                    cancellationResult = _stripeService.CancelPaymentIntentAsync(paymentIntent.Id).GetAwaiter().GetResult();
                    _logger.LogInformation($"payment intent {paymentIntent.Id} was canceled by background job");
                }
                else
                {
                    cancellationResult = OperationResult.Success(null);
                }
            } while (!cancellationResult.Succeeded && ++retryNumber < _retryPolicyNumber);
        }
    }
}
