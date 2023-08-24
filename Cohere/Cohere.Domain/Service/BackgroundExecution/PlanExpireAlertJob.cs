using Amazon.Runtime.Internal.Util;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.UnitOfWork;
using Cohere.Domain.Models.Account;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cohere.Entity.Entities;
using Cohere.Domain.Infrastructure.Generic;
using Stripe;
namespace Cohere.Domain.Service.BackgroundExecution
{
    public class PlanExpireAlertJob : IPlanExpireAlertJob
    {
        private readonly ILogger<PlanExpireAlertJob> _logger;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SubscriptionService _subscriptionService;
        private readonly ICommonService _commonService;
        public PlanExpireAlertJob(
            INotificationService notificationService,
            ILogger<PlanExpireAlertJob> logger,
            IUnitOfWork unitOfWork,
            SubscriptionService subscriptionService,
            ICommonService commonService)
        {
            _notificationService = notificationService;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _subscriptionService = subscriptionService;
            _commonService = commonService;
        }
        public void Execute(params object[] ars)
        {
            try
            {
                _logger.Log(LogLevel.Information, $"Started {nameof(PlanExpireAlertJob)} at {DateTime.UtcNow}");

                _logger.Log(LogLevel.Information, $"Sending Notification All Admin for cancelled account expiration alert at {DateTime.UtcNow}");
                var modelListAsTask = GetUserDetailsOfCancelledPlanAsync();
                var modelList = modelListAsTask.Result;

                if (modelList.Count > 0)
                    _notificationService.SendNotificationBeforeExpirationOfCancelledPlanToAdmins(modelList);
                else
                    _logger.Log(LogLevel.Information, "No user to send the Notification");
                _logger.Log(LogLevel.Information, $"Finished sending Notification to all admin about expiration of all cancelled accounts at {DateTime.UtcNow}");

            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, $"AccountExpirationJob at {DateTime.UtcNow}", e.Message);
            }
        }
        private async Task<List<CancelledPlanExpirationEmailModel>> GetUserDetailsOfCancelledPlanAsync()
        {
            var modelList = new List<CancelledPlanExpirationEmailModel>();
            var dateAfterSevenDays = DateTime.Today.AddDays(7);
            var paidTierPurchaseList = await _unitOfWork.GetRepositoryAsync<PaidTierPurchase>().GetAll();
            var expiringPaidTierPurchaseList = paidTierPurchaseList.Where(p => p.Payments.Last().PeriodEnds != null && p.Payments.Last().PeriodEnds.Value.Date == dateAfterSevenDays).ToList();//only account who is expiring after 7 days            
            var clientIdsList = expiringPaidTierPurchaseList.Select(p => p.ClientId).Distinct().ToList();
            foreach (var clientId in clientIdsList)
            {
                var allPurchasedPlan = expiringPaidTierPurchaseList.Where(p => p.ClientId == clientId).ToList();
                var currentPurchasedPlan = allPurchasedPlan.OrderByDescending(p => p.CreateTime).FirstOrDefault();
                if (currentPurchasedPlan is null)
                {
                    continue;
                }
                var subscriptionResult = await _commonService.GetProductPlanSubscriptionAsync(currentPurchasedPlan.SubscriptionId);
                var subscription = subscriptionResult.Payload;
                if (subscription.CancelAtPeriodEnd) //for only cancelled paidtier account
                {
                    DateTime cancellationDate = (DateTime)subscription.CanceledAt;
                    DateTime expireDate = (DateTime)currentPurchasedPlan.Payments.Last().PeriodEnds;

                    var clientUser = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == clientId);
                    var clientAccount = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(u => u.Id == clientUser.AccountId);
                    var customerFullName = $"{clientUser.FirstName} {clientUser.LastName}";
                    var customerEmail = clientAccount.Email;
                    var cancelledPlanExpirationEmailModel = new CancelledPlanExpirationEmailModel();
                    cancelledPlanExpirationEmailModel.customerName = customerFullName;
                    cancelledPlanExpirationEmailModel.customerEmail = customerEmail;
                    cancelledPlanExpirationEmailModel.expireDate = expireDate;
                    cancelledPlanExpirationEmailModel.cancellationDate = cancellationDate;
                    modelList.Add(cancelledPlanExpirationEmailModel);
                }
            }
            return modelList;
        }
    }
}