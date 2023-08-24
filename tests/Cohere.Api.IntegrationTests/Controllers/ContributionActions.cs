using Cohere.Domain.Models;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Contribution;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cohere.Api.IntegrationTests.Controllers
{
    public interface IStripeActions
    {
        Task<PaymentMethod> CreatePaymentMethod(string cardNumber, long expMonth, long expYear, string cvc);

        Task<PaymentIntent> GetPaymentIntentByClientSecret(string clientSecret);

        Task<PaymentIntent> ConfirmPaymentIntent(string paymentIntentId, PaymentMethod paymentMethod);
    }

    public class StripeActions : IStripeActions
    {
        private readonly PaymentMethodService _paymentMethodService;
        private readonly PaymentIntentService _paymentIntentService;

        public StripeActions(PaymentMethodService paymentMethodService, PaymentIntentService paymentIntentService)
        {
            _paymentMethodService = paymentMethodService;
            _paymentIntentService = paymentIntentService;
        }

        public async Task<PaymentMethod> CreatePaymentMethod(string cardNumber, long expMonth, long expYear, string cvc)
        {
            var options = new PaymentMethodCreateOptions
            {
                Type = "card",
                Card = new PaymentMethodCardCreateOptions
                {
                    Number = cardNumber,
                    ExpMonth = expMonth,
                    ExpYear = expYear,
                    Cvc = cvc
                }
            };

            var paymentMethod = await _paymentMethodService.CreateAsync(options);
            return paymentMethod;
        }

        public async Task<PaymentIntent> ConfirmPaymentIntent(string paymentIntentId, PaymentMethod paymentMethod)
        {
            var options = new PaymentIntentConfirmOptions()
            {
                PaymentMethod = paymentMethod.Id
            };

            return await _paymentIntentService.ConfirmAsync(paymentIntentId, options);
        }

        public async Task<PaymentIntent> GetPaymentIntentByClientSecret(string clientSecret)
        {
            return await _paymentIntentService.GetAsync(GetPaymentIntentFromClientSecret(clientSecret));
        }

        private string GetPaymentIntentFromClientSecret(string clientSecret)
        {
            return clientSecret.Split("_secret").FirstOrDefault();
        }

    }

    public static class ContributionActions
    {
        public static async Task<T> GetCoachContribtuion<T>(string contributionId, HttpClient client) where T : ContributionBaseViewModel
        {
            var result = await client.GetAsync(ApiRoutes.Contribution.Coach.GetCohealerContribById(contributionId));

            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<T>();
        }

        public static async Task<T> GetClientContribtuion<T>(string contributionId, HttpClient client)
        {
            var result = await client.GetAsync(ApiRoutes.Contribution.Client.GetClientContribById(contributionId));

            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<T>();
        }

        public static async Task ApproveContribution(string contributionId, HttpClient client)
        {
            var changeStatusBody = new AdminReviewNoteViewModel()
            {
                Status = "Approved"
            };
            var result = await client.PostAsJsonAsync(ApiRoutes.Contribution.Admin.ChagneStatus(contributionId), changeStatusBody);

            await Utils.ThrowIfNotSuccess(result);
        }

        public static async Task<ContributionBaseViewModel> CreateOneToOneContribtuion(HttpClient client)
        {
            var oneToOneContribution = Utils.LoadJson<ContributionOneToOneViewModel>("JsonData/OneToOneContribution.json");
            oneToOneContribution.Title += DateTime.UtcNow.ToString();

            var createContributionBody = System.Text.Json.JsonSerializer.Serialize(oneToOneContribution, new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var result = await client.PostAsJsonAsync(ApiRoutes.Contribution.Coach.Create, createContributionBody);

            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionBaseViewModel>();
        }

        public static async Task<ContributionBaseViewModel> CreateLiveCourseContribtuion(HttpClient client)
        {
            var liveCourseContribution = Utils.LoadJson<ContributionCourseViewModel>("JsonData/LiveCourseContribution.json");
            liveCourseContribution.Title += DateTime.UtcNow.ToString();

            var createContributionBody = System.Text.Json.JsonSerializer.Serialize(liveCourseContribution, new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            var result = await client.PostAsJsonAsync(ApiRoutes.Contribution.Coach.Create, createContributionBody);

            await Utils.ThrowIfNotSuccess(result);

            return await result.Content.ReadAsAsync<ContributionBaseViewModel>();
        }

        public static async Task EnablePackagePurchase(string contributionId, int packageSessionNumbers, int packageSessionDiscountPercentage, HttpClient client)
        {
            var liveCourseContributionRequest = await client.GetAsync(ApiRoutes.Contribution.Coach.GetCohealerContribById(contributionId));

            await Utils.ThrowIfNotSuccess(liveCourseContributionRequest);

            var contribution = await liveCourseContributionRequest.Content.ReadAsAsync<ContributionOneToOneViewModel>();

            contribution.PaymentInfo.PaymentOptions.Add(PaymentOptions.SessionsPackage.ToString());

            contribution.PaymentInfo.PackageSessionNumbers = packageSessionNumbers;
            contribution.PaymentInfo.PackageSessionDiscountPercentage = packageSessionDiscountPercentage;

            CleanServiceFields(contribution);


            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };

            var updateContributionBody = JsonConvert.SerializeObject(contribution, settings);

            var updateResult = await client.PutAsJsonAsync(ApiRoutes.Contribution.Coach.Update(contribution.Id), updateContributionBody);

            await Utils.ThrowIfNotSuccess(updateResult);
        }

        public static async Task<List<AvailabilityTime>> GetClientSlots(string contributionId, HttpClient client)
        {
            var getSlotsResult = await client.PostAsync(ApiRoutes.Contribution.Client.GetClientSlots(contributionId), null);

            await Utils.ThrowIfNotSuccess(getSlotsResult);

            return await getSlotsResult.Content.ReadAsAsync<List<AvailabilityTime>>();
        }

        public static async Task EnableSplitPayments(string contributionId, HttpClient client)
        {
            var liveCourseContributionRequest = await client.GetAsync(ApiRoutes.Contribution.Coach.GetCohealerContribById(contributionId));

            await Utils.ThrowIfNotSuccess(liveCourseContributionRequest);

            var contribution = await liveCourseContributionRequest.Content.ReadAsAsync<ContributionCourseViewModel>();

            contribution.PaymentInfo.PaymentOptions.Add(PaymentOptions.SplitPayments.ToString());

            CleanServiceFields(contribution);

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };

            var updateContributionBody = JsonConvert.SerializeObject(contribution, settings);

            var updateResult = await client.PutAsJsonAsync(ApiRoutes.Contribution.Coach.Update(contribution.Id), updateContributionBody);

            await Utils.ThrowIfNotSuccess(updateResult);
        }

        private static void CleanServiceFields(ContributionBaseViewModel contribution)
        {
            contribution.TimeZoneId = string.Empty;
            contribution.ServiceProviderName = string.Empty;
            contribution.Status = string.Empty;
        }
    }
}
