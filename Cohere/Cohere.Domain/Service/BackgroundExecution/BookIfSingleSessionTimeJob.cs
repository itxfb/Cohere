using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Cohere.Entity.Enums.Payments;
using System.Collections.Generic;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class BookIfSingleSessionTimeJob : IBookIfSingleSessionTimeJob
    {
        private readonly ILogger<BookIfSingleSessionTimeJob> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IContributionRootService _contributionRootService;
        private readonly IContributionBookingService _contributionBookingService;

        public BookIfSingleSessionTimeJob(
            ILogger<BookIfSingleSessionTimeJob> logger,
            IUnitOfWork unitOfWork,
            IContributionRootService contributionRootService,
            IContributionBookingService contributionBookingService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _contributionRootService = contributionRootService;
            _contributionBookingService = contributionBookingService;
        }

        public async Task ExecuteAsync(params object[] args)
        {
            _logger.LogInformation($"{nameof(BookIfSingleSessionTimeJob)} start executing");

            try
            {
                var contributionId = args[0] as string;
                var clientPurchaseId = args[1] as string;
                var transactionId = args[2] as string;
                var userAccountId = args[3] as string;
                var autobookingforFreeContrib = Convert.ToBoolean(args[4]);
                await BookIfSingleSessionAsync(contributionId, clientPurchaseId, transactionId, userAccountId,autobookingforFreeContrib);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error during executing {nameof(BookIfSingleSessionTimeJob)}");
            }
        }

        private async Task BookIfSingleSessionAsync(string contributionId, string clientPurchaseId, string transactionId, string userAccountId, bool autobookingforFreeContrib)
        {
            try
            {
                var contribution = await _contributionRootService.GetOne(contributionId);

                if (!(contribution is SessionBasedContribution course))
                {
                    _logger.LogError($"Only {nameof(ContributionCourse)} and {nameof(ContributionMembership)} and {nameof(ContributionCommunity)} supported for {nameof(BookIfSingleSessionTimeJob)}");
                    return;
                }

                bool isAutobookingEnabled = false;
                if (autobookingforFreeContrib)
                {
                    isAutobookingEnabled = true;
                }
                else
                {
                    var clientPurchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(e => e.Id == clientPurchaseId);
                    var payment = clientPurchase.Payments.First(e => e.TransactionId == transactionId);
                    isAutobookingEnabled = (payment.PaymentStatus == PaymentStatus.Succeeded || payment.PurchaseAmount == 0 && payment.PaymentStatus == PaymentStatus.Paid)
                    && clientPurchase.Payments.Count(p => (p.PaymentStatus == PaymentStatus.Succeeded || p.PurchaseAmount == 0 && p.PaymentStatus == PaymentStatus.Paid)) == 1;
                    if (isAutobookingEnabled == false)
                    {
                        isAutobookingEnabled = payment.IsTrial == true && payment.PaymentStatus == PaymentStatus.Paid;
                    }
                }
                if (isAutobookingEnabled)
                {
                    var sessionTimesToBook = course.Sessions.Where(session => !session.IsPrerecorded && session.SessionTimes.Count == 1);
                    var bookSessionTimeModels = new List<BookSessionTimeViewModel>();
                    foreach (var session in sessionTimesToBook)
                    {
                        if (!session.SessionTimes.FirstOrDefault().IsCompleted)
                        {
                            var sessionTime = session.SessionTimes.First();

                            var model = new BookSessionTimeViewModel()
                            {
                                ContributionId = contribution.Id,
                                SessionId = session.Id,
                                SessionTimeId = sessionTime.Id,
                            };
                            bookSessionTimeModels.Add(model);
                        }
                    }

                    try
                    {
                        if (bookSessionTimeModels.Count > 0)
                        {
                            _contributionBookingService.BookSessionTimeAsync(bookSessionTimeModels, userAccountId);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "error during booking single session time");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error during booking single session time in BookIfSingleSessionAsync with contributionId: {contributionId}  and ClientId: {userAccountId}");
            }
        }
    }
}
