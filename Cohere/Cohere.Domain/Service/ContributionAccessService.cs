using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Service.Abstractions;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Entities.Invoice;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Stripe;
using Account = Cohere.Entity.Entities.Account;

namespace Cohere.Domain.Service
{
    public class ContributionAccessService : IContributionAccessService
    {
        private readonly ILogger<ContributionAccessService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IContributionRootService _contributionRootService;
        private readonly IContributionBookingService _contributionBookingService;
        private readonly IStripeService _stripeService;
        private readonly ProductService _productService;
        private readonly SubscriptionService _subscriptionService;
        private readonly IMemoryCache _memoryCache;
        private readonly ContributionPurchaseService _contributionPurchaseService;
        private readonly IProfilePageService _profilePageService;
        private readonly ICommonService _commonService;
        private readonly IChatService _chatService;


        public ContributionAccessService(ILogger<ContributionAccessService> logger, IUnitOfWork unitOfWork, IMapper mapper,
            IContributionRootService contributionRootService, IContributionBookingService contributionBookingService,
            IStripeService stripeService, ProductService productService, SubscriptionService subscriptionService,
            IMemoryCache memoryCache, ContributionPurchaseService contributionPurchaseService,
            IProfilePageService profilePageService, ICommonService commonService, IChatService chatService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _contributionRootService = contributionRootService;
            _contributionBookingService = contributionBookingService;
            _stripeService = stripeService;
            _productService = productService;
            _subscriptionService = subscriptionService;
            _memoryCache = memoryCache;
            _contributionPurchaseService = contributionPurchaseService;
            _profilePageService = profilePageService;
            _commonService = commonService;
            _chatService = chatService;
        }

        public async Task<OperationResult<AccessCode>> CreateAccessCode(string contributionId, string accountId,
            int validPeriodInYears)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .GetOne(e => e.Id == contributionId);

            if (contribution is null)
            {
                return OperationResult<AccessCode>.Failure("contribution not found");
            }

            var contributionBaseViewModel = _mapper.Map<ContributionBaseViewModel>(contribution);

            var creatorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            if (!contributionBaseViewModel.IsOwnerOrPartner(creatorUser.Id))
            {
                return OperationResult<AccessCode>.Failure("forbidden");
            }

            var inserted = await _unitOfWork.GetRepositoryAsync<AccessCode>().Insert(new AccessCode()
            {
                Code = Guid.NewGuid().ToString(),
                ContributionId = contribution.Id,
                CreatorId = creatorUser.Id,
                ValidTill = DateTime.UtcNow.AddYears(validPeriodInYears)
            });

            return OperationResult<AccessCode>.Success(inserted);
        }

        public async Task<OperationResult> GrantAccessByAccessCode(string clientAccountId, string contributionId, string accessCode)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == clientAccountId);
            var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(e => e.Id == clientAccountId);
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .GetOne(e => e.Id == contributionId);

            if (string.IsNullOrEmpty(accessCode))
            {
                throw new Exception("access code is null");
            }

            var accessCodeModel =
                await _unitOfWork.GetRepositoryAsync<AccessCode>()
                    .GetOne(e => e.Code == accessCode && e.ContributionId == contributionId);

            if (accessCodeModel is null)
            {
                throw new Exception("access code not found");
            }

            if (accessCodeModel.ValidTill < DateTime.UtcNow)
            {
                throw new Exception("access code not valid");
            }

            var enrollmentResult = await EnrolFreeContribution(contribution, clientAccountId, accessCode);
            if (enrollmentResult.Succeeded)
            {
                return OperationResult.Success(enrollmentResult.Message, enrollmentResult.Payload);
            }

            return OperationResult.Failure(enrollmentResult.Message, enrollmentResult.Payload);
        }

        private async Task<OperationResult> EnrolFreeContribution(ContributionBase contribution, string clientAccountId, string accessCode)
        {
            if (contribution.Type is nameof(ContributionCommunity))
            {
                var result = await _contributionPurchaseService.SubscribeToCommunityContributionAsync(clientAccountId, contribution.Id, null, PaymentOptions.Free, accessCode);
                if (result.Succeeded)
                {
                    //Add follower in Profile Page
                    try
                    {
                        var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
                        await _profilePageService.AddFollowerToProfile(clientAccountId, contributor.AccountId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Unable to add folower in profile page : {ex.Message} for client ", clientAccountId, DateTime.Now.ToString("F"));
                    }
                    return OperationResult.Success("Purchased Free community contribution", result.Payload);
                }
            }
            else if (contribution.Type is nameof(ContributionMembership))
            {
                var result = await _contributionPurchaseService.SubscribeToMembershipContributionAsync(clientAccountId, contribution.Id, null, PaymentOptions.Free, accessCode);
                if (result.Succeeded)
                {
                    //Add follower in Profile Page
                    try
                    {
                        var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
                        await _profilePageService.AddFollowerToProfile(clientAccountId, contributor.AccountId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Unable to add folower in profile page : {ex.Message} for client ", clientAccountId, DateTime.Now.ToString("F"));
                    }

                    return OperationResult.Success("Purchased Free membership contribution", result.Payload);
                }
            }
            else if (contribution.Type is nameof(ContributionCourse))
            {
                var result = await _contributionPurchaseService.PurchaseLiveCourseWithCheckout(contribution.Id, null, clientAccountId, PaymentOptions.Free, null, accessCode);
                if (result.Succeeded)
                {
                    //Add follower in Profile Page
                    try
                    {
                        var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
                        await _profilePageService.AddFollowerToProfile(clientAccountId, contributor.AccountId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Unable to add folower in profile page : {ex.Message} for client ", clientAccountId, DateTime.Now.ToString("F"));
                    }
                    return OperationResult.Success("Purchased Free group course contribution", result.Payload);
                }
            }
            return OperationResult.Failure("Error during enrollment");
        }

        public async Task<OperationResult> CancelAccess(string accountId, string contributionId, string participantId)
        {
            var coach = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);
            var contribution =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(e => e.Id == contributionId);

            if (contribution.UserId != coach.Id && contribution.Partners?.Any(a => a.UserId == coach.Id) == false)
            {
                return OperationResult.Failure("Not allowed");
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .Get(p => p.ClientId == participantId && p.ContributionId == contribution.Id);
            var lastPurchaseVm = _mapper.Map<PurchaseViewModel>(purchases.OrderByDescending(e => e.CreateTime).FirstOrDefault());

            string standardAccountId = string.Empty;
            try
            {
                if (contribution.PaymentType == PaymentTypes.Advance)
                {
                    var user = _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.Id == contribution.UserId).GetAwaiter().GetResult();
                    standardAccountId = user.StripeStandardAccountId;
                }
                lastPurchaseVm.FetchActualPaymentStatuses(new Dictionary<string, string>() { { contribution.Id, standardAccountId } });

                if (lastPurchaseVm.RecentPaymentOption != PaymentOptions.Trial || !lastPurchaseVm.HasActiveSubscription)
                {
                    //return OperationResult.Failure("participant has no active subscription");
                }

                //in case of split payment subscription need to be cancelled from stripe as well
                if (lastPurchaseVm.HasActiveSubscription)
                {
                    await _stripeService.CancelSubscriptionImmediately(lastPurchaseVm.SubscriptionId, standardAccountId);
                    _memoryCache.Remove("subscription_" + lastPurchaseVm.SubscriptionId);
                }
            }
            catch
            {
            }
            foreach (var purchase in purchases.ToList())
            {
                purchase.ClientId = $"Delete-{purchase.ClientId}";
                purchase.Payments.LastOrDefault().IsAccessRevokedByCoach = true;
                await _unitOfWork.GetRepositoryAsync<Purchase>().Update(purchase.Id, purchase, true);
            }


            try
            {
                if (contribution is SessionBasedContribution sessionBasedContribution)
                {
                    var chat = sessionBasedContribution.Chat.CohealerPeerChatSids.Where(c => c.Key == participantId).Select(s => s.Value).FirstOrDefault();
                    if (chat != null)
                    {
                        await _chatService.DeleteChatForContribution(chat);
                        await _chatService.RemoveUserFromChat(participantId, contribution.Chat.Sid);
                        sessionBasedContribution.Chat.CohealerPeerChatSids.Remove(participantId);
                    }
                }
            }
            catch { }

            //check if the contribution is a session based contribution and remove client from session too
            _commonService.RemoveUserFromContributionSessions(contribution, participantId);


            var invoiceExisted = _commonService.GetInvoiceIfExist(participantId, contributionId, lastPurchaseVm.RecentPaymentOption.ToString());
            if (invoiceExisted != null)
            {
                invoiceExisted.IsCancelled = true;
                await _unitOfWork.GetRepositoryAsync<StripeInvoice>().Update(invoiceExisted.Id, invoiceExisted);
            }

            return OperationResult.Success();
        }

        private async Task<string> GetOrCreateProduct(ContributionBase contribution)
        {
            try
            {
                var product = await _productService.GetAsync(contribution.Id);
                return product.Id;
            }
            catch (StripeException ex)
            {
                if (ex.StripeError.Code != "resource_missing") throw;

                var product = await _stripeService.CreateProductAsync(new CreateProductViewModel()
                {
                    Id = contribution.Id,
                    Name = contribution.Title,
                });

                return product.Payload;
            }
        }
    }
}