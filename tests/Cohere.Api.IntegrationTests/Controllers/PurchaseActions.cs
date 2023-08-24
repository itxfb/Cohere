using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.Payment;
using Cohere.Entity.Enums.Contribution;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests.Controllers
{
    public static class PurchaseActions
    {
        public static async Task<ContributionPaymentIntentDetailsViewModel> PurchaseEntireCourse(string contributionId, HttpClient client)
        {
            var purchaseBody = new PurchaseCourseContributionViewModel()
            {
                ContributionId = contributionId,
                PaymentOptions = PaymentOptions.EntireCourse.ToString()
            };

            var result = await client.PostAsJsonAsync(ApiRoutes.Purchase.PurchaseLiveCourse, purchaseBody);
            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionPaymentIntentDetailsViewModel>();
        }

        public static async Task<ContributionPaymentIntentDetailsViewModel> PurchaseSplitPaymentsCourse(string contributionId, string paymentMethodId, HttpClient client)
        {
            var purchaseBody = new PurchaseCourseContributionViewModel()
            {
                ContributionId = contributionId,
                PaymentOptions = PaymentOptions.SplitPayments.ToString(),
                PaymentMethodId = paymentMethodId
            };

            var result = await client.PostAsJsonAsync(ApiRoutes.Purchase.PurchaseLiveCourse, purchaseBody);
            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionPaymentIntentDetailsViewModel>();
        }

        public static async Task<ContributionPaymentIntentDetailsViewModel> PurchaseOneToOneSession(string contributionId, string availabilityTimeId, HttpClient client)
        {
            var purchaseBody = new BookOneToOneTimeViewModel()
            {
                ContributionId = contributionId,
                AvailabilityTimeId = availabilityTimeId
            };

            var result = await client.PostAsJsonAsync(ApiRoutes.Purchase.PurchaseOneToOneSession, purchaseBody);
            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionPaymentIntentDetailsViewModel>();
        }

        public static async Task<ContributionPaymentIntentDetailsViewModel> PurchaseOneToOnePackage(string contributionId, HttpClient client)
        {
            var purchaseBody = new PurchaseOneToOnePackageViewModel()
            {
                ContributionId = contributionId
            };

            var result = await client.PostAsJsonAsync(ApiRoutes.Purchase.PurchaseOntToOnePackage, purchaseBody);
            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionPaymentIntentDetailsViewModel>();
        }
    }
}
