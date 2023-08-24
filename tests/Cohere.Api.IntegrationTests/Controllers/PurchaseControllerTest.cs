using Cohere.Domain.Models.ContributionViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stripe;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests.Controllers
{
    [DoNotParallelize]
    [TestClass]
    public class PurchaseControllerTest : IntegrationTestBase
    {
        private IStripeActions _stripeActions => AppServices.GetService<IStripeActions>();

        [TestMethod]
        public async Task PurchaseEntireCourse()
        {
            SetCoachAuthToken();
            var createdContribution = await ContributionActions.CreateLiveCourseContribtuion(Client);

            SetAdminAuthToken();
            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetClientAuthToken();

            var clientContribution = await ContributionActions.GetClientContribtuion<ContributionCourseViewModel>(createdContribution.Id, Client);
            var paymentIntent = await PurchaseActions.PurchaseEntireCourse(clientContribution.Id, Client);

            Assert.IsTrue(!string.IsNullOrEmpty(paymentIntent.ClientSecret));

            var updatedPaymentIntent = await _stripeActions.GetPaymentIntentByClientSecret(paymentIntent.ClientSecret);

            var confirmedPaymentIntent = await _stripeActions.ConfirmPaymentIntent(updatedPaymentIntent.Id, await GetValidTestPaymentMethod());

            Assert.IsTrue(confirmedPaymentIntent.Status == "succeeded");

            var purchasedContribution = await ContributionActions.GetClientContribtuion<ContributionCourseViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(purchasedContribution.IsPurchased);
        }

        [TestMethod]
        public async Task PurchaseSplitSessionsCourse()
        {
            SetCoachAuthToken();
            var createdContribution = await ContributionActions.CreateLiveCourseContribtuion(Client);
            await ContributionActions.EnableSplitPayments(createdContribution.Id, Client);

            SetAdminAuthToken();
            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetClientAuthToken();

            var clientContribution = await ContributionActions.GetClientContribtuion<ContributionCourseViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(clientContribution.IsPurchased == false);

            var createdPaymentMethod = await GetValidTestPaymentMethod();
            var paymentIntent = await PurchaseActions.PurchaseSplitPaymentsCourse(clientContribution.Id, createdPaymentMethod.Id, Client);

            var updatedPaymentIntent = await _stripeActions.GetPaymentIntentByClientSecret(paymentIntent.ClientSecret);

            var confirmedPaymentIntent = await _stripeActions.ConfirmPaymentIntent(updatedPaymentIntent.Id, createdPaymentMethod);

            Assert.IsTrue(confirmedPaymentIntent.Status == "succeeded");

            var purchasedContribution = await ContributionActions.GetClientContribtuion<ContributionCourseViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(purchasedContribution.IsPurchased);
        }

        [TestMethod]
        public async Task PurchaseOneToOneSession()
        {
            SetCoachAuthToken();
            var createdContribution = await ContributionActions.CreateOneToOneContribtuion(Client);

            SetAdminAuthToken();
            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetClientAuthToken();

            var clientContribution = await ContributionActions.GetClientContribtuion<ContributionOneToOneViewModel>(createdContribution.Id, Client);

            var bookedTimes = await ContributionActions.GetClientSlots(createdContribution.Id, Client);

            var createdPaymentMethod = await GetValidTestPaymentMethod();

            var paymentIntent = await PurchaseActions.PurchaseOneToOneSession(clientContribution.Id, bookedTimes.FirstOrDefault().Id, Client);

            var updatedPaymentIntent = await _stripeActions.GetPaymentIntentByClientSecret(paymentIntent.ClientSecret);

            var confirmedPaymentIntent = await _stripeActions.ConfirmPaymentIntent(updatedPaymentIntent.Id, createdPaymentMethod);

            Assert.IsTrue(confirmedPaymentIntent.Status == "succeeded");

            var purchasedContribution = await ContributionActions.GetClientContribtuion<ContributionOneToOneViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(purchasedContribution.IsPurchased);
        }

        [TestMethod]
        public async Task PurchaseOneToOnePackage()
        {
            SetCoachAuthToken();
            var createdContribution = await ContributionActions.CreateOneToOneContribtuion(Client);
            const int PackageSessionNumbers = 5;
            const int PackageSessionDiscountPercentage = 10;
            await ContributionActions.EnablePackagePurchase(createdContribution.Id, PackageSessionNumbers, PackageSessionDiscountPercentage, Client);

            SetAdminAuthToken();
            await ContributionActions.ApproveContribution(createdContribution.Id, Client);

            SetClientAuthToken();

            var clientContribution = await ContributionActions.GetClientContribtuion<ContributionOneToOneViewModel>(createdContribution.Id, Client);

            var paymentIntent = await PurchaseActions.PurchaseOneToOnePackage(clientContribution.Id, Client);

            var updatedPaymentIntent = await _stripeActions.GetPaymentIntentByClientSecret(paymentIntent.ClientSecret);

            var createdPaymentMethod = await GetValidTestPaymentMethod();

            var confirmedPaymentIntent = await _stripeActions.ConfirmPaymentIntent(updatedPaymentIntent.Id, createdPaymentMethod);

            Assert.IsTrue(confirmedPaymentIntent.Status == "succeeded");

            var purchasedContribution = await ContributionActions.GetClientContribtuion<ContributionOneToOneViewModel>(createdContribution.Id, Client);

            Assert.IsTrue(purchasedContribution.IsPurchased);
        }

        private async Task<PaymentMethod> GetValidTestPaymentMethod()
        {
            return await _stripeActions.CreatePaymentMethod("4242424242424242", 1, DateTime.UtcNow.AddYears(2).Year, "202");
        }
    }
}
