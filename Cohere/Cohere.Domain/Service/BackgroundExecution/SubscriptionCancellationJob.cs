using System;
using System.Threading;
using Cohere.Domain.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.Enums.Payments;
using Stripe;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class SubscriptionCancellationJob : ISubscriptionCancellationJob
    {
        public const string RetryPolicyNumber = "SubscriptionCancellationJobRetryPolicy";

        private readonly IStripeService _stripeService;
        private readonly int _retryPolicyNumber;

        public SubscriptionCancellationJob(IStripeService stripeService, Func<string, int> integersResolver)
        {
            _stripeService = stripeService;
            _retryPolicyNumber = integersResolver.Invoke(RetryPolicyNumber);
        }

        public void Execute(params object[] args)
        {
            var retryNumber = 0;
            OperationResult cancellationResult = null;
            string subscriptionId = (string) args[0];
            string standardAccountId = (string) args[1];
            do
            {
                if (retryNumber > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                }

                cancellationResult = _stripeService.GetProductPlanSubscriptionAsync(subscriptionId, standardAccountId).GetAwaiter().GetResult();

                if (!cancellationResult.Succeeded)
                {
                    continue;
                }

                var subscription = (Subscription)cancellationResult.Payload;

                if (subscription.LatestInvoice.PaymentIntent.Status == PaymentStatus.Succeeded.GetName())
                {
                    continue;
                }

                if (subscription.Status != "canceled")
                {
                    cancellationResult = _stripeService.CancelProductPlanSubscriptionScheduleAsync(subscription.Schedule.Id, standardAccountId).GetAwaiter().GetResult();

                    if (!cancellationResult.Succeeded)
                    {
                        continue;
                    }
                }

                var latestInvoice = subscription.LatestInvoice;

                if (latestInvoice != null && latestInvoice.Status != "draft" && latestInvoice.Status != "void")
                {
                    cancellationResult = _stripeService.VoidInvoiceAsync(subscription.LatestInvoiceId, standardAccountId).GetAwaiter().GetResult();
                }
            }
            while (!cancellationResult.Succeeded && ++retryNumber < _retryPolicyNumber);
        }
    }
}
