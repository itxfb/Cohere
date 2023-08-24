using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Transform;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models;
using Cohere.Domain.Models.ContributionViewModels;
using Cohere.Domain.Models.ContributionViewModels.ForAdmin;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer.Tables;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.Payment.Stripe;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.ActiveCampaign;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Membership;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Enums;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Cohere.Entity.Utils;
using Ical.Net.CalendarComponents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.Cms;
using RestSharp;
using Stripe;
using Twilio.TwiML.Messaging;
using static Cohere.Domain.Utils.Constants;
using Account = Cohere.Entity.Entities.Account;


namespace Cohere.Domain.Service
{
    public class ContributionService : IContributionService
    {
        private readonly ICommonService _commonService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notifictionService;
        private readonly IFileStorageManager _fileManager;
        private readonly IStripeService _stripeService;
        private readonly IPaymentSystemFeeService _paymentSystemFeeService;
        private readonly IChatService _chatService;
        private readonly ILogger<ContributionService> _logger;
        private readonly IPricingCalculationService _pricingeCalculationService;
        private readonly IJobScheduler _jobScheduler;
        private readonly ICohealerIncomeService _cohealerIncomeService;
        private readonly INoteService _noteService;
        private readonly IContributionRootService _contributionRootService;
        private readonly IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> _paidTiersService;
        private readonly IActiveCampaignService _activeCampaignService;
        private readonly IPodService _podService;
        private readonly IZoomService _zoomService;
        private readonly IContributionBookingService _contributionBookingService;
        private readonly IFCMService _fcmService;
        private readonly double _escrowPeriodSeconds;
        private readonly decimal _maxAllowedCostAmount;
        private readonly double _affiliateEscrowPeriodSeconds;
        private readonly S3Settings _s3SettingsOptions;
        private readonly IProfilePageService _profilePageService;

        private static Dictionary<PaymentSplitPeriods, string> StripePlanIntervals =
            new Dictionary<PaymentSplitPeriods, string>
            {
                { PaymentSplitPeriods.Monthly, "month" },
                { PaymentSplitPeriods.Weekly, "week" },
                { PaymentSplitPeriods.Daily, "day" },
                { PaymentSplitPeriods.Yearly, "year" }
            };

        private static Dictionary<PaymentOptions, string> StripePlanIntervalByPaymentOptions =
            new Dictionary<PaymentOptions, string>()
            {
                { PaymentOptions.DailyMembership, "day" },
                { PaymentOptions.WeeklyMembership, "week" },
                { PaymentOptions.MonthlyMembership, "month" },
                { PaymentOptions.YearlyMembership, "year" }
            };

        private static ICollection<string> ContributionTypes = new List<string>
        {
            nameof(ContributionCourse),
            nameof(ContributionOneToOne),
            nameof(ContributionMembership),
            nameof(ContributionCommunity),
        };

        public ContributionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            INotificationService notificationService,
            IFileStorageManager fileManager,
            IStripeService stripeService,
            IPaymentSystemFeeService paymentSystemFeeService,
            IChatService chatService,
            ILogger<ContributionService> logger,
            IPricingCalculationService pricingeCalculationService,
            IJobScheduler jobScheduler,
            ICohealerIncomeService cohealerIncomeService,
            INoteService noteService,
            IContributionRootService contributionRootService,
            IActiveCampaignService activeCampaignService,
            IOptions<PaymentSettings> paymentSettings,
            IOptions<AffiliateSettings> affiliateSettings,
            IPaidTiersService<PaidTierOptionViewModel, PaidTierOption> paidTiersService,
            IPodService podService,
            IZoomService zoomService, IContributionBookingService contributionBookingService, ICommonService commonService, IFCMService fcmService, IOptions<S3Settings> s3SettingsOptions,
            IProfilePageService profilePageService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _notifictionService = notificationService;
            _fileManager = fileManager;
            _stripeService = stripeService;
            _paymentSystemFeeService = paymentSystemFeeService;
            _chatService = chatService;
            _logger = logger;
            _pricingeCalculationService = pricingeCalculationService;
            _jobScheduler = jobScheduler;
            _cohealerIncomeService = cohealerIncomeService;
            _noteService = noteService;
            _contributionRootService = contributionRootService;
            _activeCampaignService = activeCampaignService;
            _paidTiersService = paidTiersService;
            _escrowPeriodSeconds = paymentSettings.Value.EscrowPeriodSeconds;
            _maxAllowedCostAmount = paymentSettings.Value.MaxCostAmountInCurrencyUnit;
            _affiliateEscrowPeriodSeconds = affiliateSettings.Value.EscrowPeriodSeconds;
            _podService = podService;
            _zoomService = zoomService;
            _contributionBookingService = contributionBookingService;
            _commonService = commonService;
            _fcmService = fcmService;
            _s3SettingsOptions = s3SettingsOptions?.Value;
            _profilePageService = profilePageService;
        }

        public async Task<OperationResult> Insert(ContributionBaseViewModel viewModel, string creatorAccountId)
        {
            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == creatorAccountId);

            if (contributorUser.Id != viewModel.UserId)
            {
                return OperationResult.Failure("It is not allowed to create contribution for other author");
            }

            viewModel.ConvertAllOwnZonedTimesToUtc(contributorUser.TimeZoneId);
            viewModel.AssignIdsToTimeRanges();

            var insertValidationResult = InsertValidations(viewModel);
            if (insertValidationResult.Failed)
            {
                return insertValidationResult;
            }

            var contribution = _mapper.Map<ContributionBase>(viewModel);

            contribution.Status = contributorUser.TransfersEnabled ? ContributionStatuses.InReview : ContributionStatuses.InSandbox;

            var insertedContributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Insert(contribution);

            var insertedViewModel = _mapper.Map<ContributionBaseViewModel>(insertedContributionBase);

            insertedViewModel.ConvertAllOwnUtcTimesToZoned(contributorUser.TimeZoneId);

            insertedViewModel.Type = viewModel.Type;

            await SendNotifications(insertedContributionBase);

            ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
            ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
            {
                CohereAccountId = contributorUser.AccountId,
                ContributionStatus = EnumHelper<ContributionStatus>.GetDisplayValue(ContributionStatus.Draft),

            };
            _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);

            return OperationResult.Success("Success", insertedViewModel);
        }

        public async Task<OperationResult<ContributionBaseViewModel>> InsertUnfinished(ContributionBaseViewModel viewModel, string creatorAccountId)
        {
            var creatorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == creatorAccountId);

            if (creatorUser.Id != viewModel.UserId)
            {
                return OperationResult<ContributionBaseViewModel>.Failure("It is not allowed to create contribution for other author");
            }

            viewModel.Status = ContributionStatuses.Draft.ToString();

            viewModel.PaymentType = creatorUser?.DefaultPaymentMethod.ToString();

            viewModel.ConvertAllOwnZonedTimesToUtc(creatorUser.TimeZoneId);

            var contribution = _mapper.Map<ContributionBase>(viewModel);

            var insertedContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Insert(contribution);

            var insertedViewModel = _mapper.Map<ContributionBaseViewModel>(insertedContribution);
            insertedViewModel.ConvertAllOwnUtcTimesToZoned(creatorUser.TimeZoneId);
            insertedViewModel.Type = viewModel.Type;

            ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
            ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
            {
                CohereAccountId = creatorUser.AccountId,
                ContributionStatus = EnumHelper<ContributionStatus>.GetDisplayValue(ContributionStatus.Draft),

            };

            if (insertedViewModel.AvailableCurrencies == null && !string.IsNullOrEmpty(creatorUser?.CountryId))
            {
                var availableCurrencies = await GetCurrenciesForContribution(creatorUser.CountryId);
                insertedViewModel.AvailableCurrencies = availableCurrencies.ToList();
            }

            _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
            return OperationResult<ContributionBaseViewModel>.Success(insertedViewModel);
        }

        public async Task<OperationResult<ContributionBaseViewModel>> UpdateUnfinished(ContributionBaseViewModel viewModel, string creatorAccountId)
        {
            var creatorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == creatorAccountId);

            var contributionExisted = await _contributionRootService.GetOne(viewModel.Id);

            if (contributionExisted == null)
            {
                return OperationResult<ContributionBaseViewModel>.Failure($"Contribution with following Id is not found: {viewModel.Id}");
            }

            if (creatorUser.Id != viewModel.UserId)
            {
                return OperationResult<ContributionBaseViewModel>.Failure("It is not allowed to edit contribution for other author");
            }

            if (contributionExisted.Status != ContributionStatuses.Draft)
            {
                return OperationResult<ContributionBaseViewModel>.Failure("Only unfinished contribution can be edited");
            }

            viewModel.Status = ContributionStatuses.Draft.ToString();

            viewModel.ConvertAllOwnZonedTimesToUtc(creatorUser.TimeZoneId);

            var contribution = _mapper.Map<ContributionBase>(viewModel);

            var updatedContributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            var updatedViewModel = _mapper.Map<ContributionBaseViewModel>(updatedContributionBase);

            updatedViewModel.ConvertAllOwnUtcTimesToZoned(creatorUser.TimeZoneId);
            updatedViewModel.Type = viewModel.Type;

            if (updatedViewModel.AvailableCurrencies == null && !string.IsNullOrEmpty(creatorUser?.CountryId))
            {
                var availableCurrencies = await GetCurrenciesForContribution(creatorUser.CountryId);
                updatedViewModel.AvailableCurrencies = availableCurrencies.ToList();
            }

            return OperationResult<ContributionBaseViewModel>.Success(updatedViewModel);
        }

        public async Task<OperationResult> DeleteUnfinished(string contributionId, string requestorAccountId)
        {
            var creatorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            var contributionExisted = await _contributionRootService.GetOne(contributionId);

            if (contributionExisted == null)
            {
                return OperationResult.Failure($"Contribution with following Id is not found: {contributionId}");
            }

            if (contributionExisted.UserId != creatorUser.Id)
            {
                OperationResult.Failure("It is not allowed to edit contribution for other author");
            }

            if (contributionExisted.Status != ContributionStatuses.Draft)
            {
                OperationResult.Failure("Only unfinished contribution can be deleted");
            }

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Delete(contributionId);

            return OperationResult.Success();
        }
        public async Task<OperationResult> DeleteContribution(string contributionId, string requestorAccountId)
        {
            var creatorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            var contributionExisted = await _contributionRootService.GetOne(contributionId);

            if (contributionExisted == null)
            {
                return OperationResult.Failure($"Contribution with following Id is not found: {contributionId}");
            }

            if (contributionExisted.UserId != creatorUser.Id)
            {
                return OperationResult.Failure("It is not allowed to edit contribution for other author");
            }
            var contributionCohealerVm = _mapper.Map<ContributionBaseViewModel>(contributionExisted);
            
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(x => x.ContributionId == contributionCohealerVm.Id);
            contributionCohealerVm.DeletingAllowed = purchase == null ? true : false;
            
            if (!contributionCohealerVm.DeletingAllowed)
            {
                return OperationResult.Failure("It is not allowed to delete the contribution with purchases");
            }

            var Posts = await _unitOfWork.GetRepositoryAsync<Post>().Get(x => x.ContributionId == contributionExisted.Id);
            if (Posts.Any())
            {
                foreach(var post in Posts)
                {
                    var postComments = await _unitOfWork.GetRepositoryAsync<Comment>().Get(x => x.PostId == post.Id);
                    if (postComments.Any())
                    {
                        foreach(var postcomment in postComments)
                        {
                            await _unitOfWork.GetRepositoryAsync<Comment>().Delete(postcomment.Id);
                        }
                    }

                    await _unitOfWork.GetRepositoryAsync<Post>().Delete(post.Id);
                }
            }

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Delete(contributionId);

            return OperationResult.Success();
        }

        public async Task<OperationResult> Update(ContributionBaseViewModel viewModel, string requesterAccountId)
        {
            var requesterUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requesterAccountId);
            var isRequesterPartner = viewModel.Partners.Any(x => x.IsAssigned && x.UserId == requesterUser.Id);
            if (requesterUser.Id != viewModel.UserId && !isRequesterPartner)
            {
                return OperationResult.Failure("It is not allowed to update contribution that is not yours");
            }

            var contributionExisted = await _contributionRootService.GetOne(viewModel.Id);

            if (contributionExisted == null)
            {
                return OperationResult.Failure($"Contribution with following Id is not found: {viewModel.Id}");
            }

            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>()
                .GetOne(e => e.Id == contributionExisted.UserId);

            var contributorAccount = await _unitOfWork.GetRepositoryAsync<Account>()
                .GetOne(e => e.Id == contributorUser.AccountId);

            bool isFreeOnly = (viewModel is ContributionCommunityViewModel || viewModel is ContributionMembershipViewModel) &&
                    contributionExisted.PaymentInfo.PaymentOptions?.Count() == 1 &&
                    contributionExisted.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free);

            
            if (contributionExisted.Status == ContributionStatuses.Approved)
            {
                if (viewModel.InvitationOnly != contributionExisted.InvitationOnly)
                {
                    return OperationResult.Failure($"changing {nameof(contributionExisted.InvitationOnly)} not allowed");
                }

                if (viewModel.PaymentInfo.CoachPaysStripeFee != contributionExisted.PaymentInfo.CoachPaysStripeFee)
                {
                    return OperationResult.Failure($"changing {nameof(contributionExisted.PaymentInfo.CoachPaysStripeFee)} not allowed");
                }

                if (contributionExisted is ContributionMembership || contributionExisted is ContributionCommunity)
                {
                    SessionBasedContribution sessionBasedContribution;
                    if (contributionExisted is ContributionMembership)
                    {
                        sessionBasedContribution = (ContributionMembership)contributionExisted;
                    }
                    else
                    {
                        sessionBasedContribution = (ContributionCommunity)contributionExisted;
                    }

                    viewModel.PaymentInfo.MembershipInfo.ProductBillingPlans =
                        sessionBasedContribution.PaymentInfo.MembershipInfo.ProductBillingPlans?
                            .ToDictionary(e => e.Key.ToString(), e => e.Value);

                    var newPrices = viewModel.PaymentInfo.MembershipInfo.Costs;
                    var existedPrices = sessionBasedContribution.PaymentInfo.MembershipInfo.Costs
                        .ToDictionary(e => e.Key.ToString(), e => e.Value);

                    bool hasFreePriceOptionOnly = existedPrices?.Count() == 0 &&
                        isFreeOnly;
                    var pricesEquals = newPrices.Count == existedPrices.Count && !newPrices.Except(existedPrices).Any();

                    if (!hasFreePriceOptionOnly && !pricesEquals)
                    {
                        return OperationResult.Failure("Changing prices not allowed");
                    }

                    var newPaymentOptions = viewModel.PaymentInfo.PaymentOptions;
                    var existedPaymentOptions = sessionBasedContribution.PaymentInfo.PaymentOptions
                        .Select(e => e.ToString())
                        .ToList();

                    var paymentOptionsEquals = newPaymentOptions.Count == existedPaymentOptions.Count
                        && !newPaymentOptions.Except(existedPaymentOptions).Any();

                    if (!hasFreePriceOptionOnly && !paymentOptionsEquals)
                    {
                        return OperationResult.Failure("Changing payment options not allowed");
                    }

                    if (!hasFreePriceOptionOnly && viewModel.PaymentInfo.CoachPaysStripeFee !=
                        sessionBasedContribution.PaymentInfo.CoachPaysStripeFee)
                    {
                        return OperationResult.Failure("Changing Fee not allowed");
                    }
                }

                if (contributionExisted.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments))
                {
                    var validationResult = await ValidateSplitPaymentsOptions(viewModel, contributionExisted);

                    if (validationResult.Failed)
                    {
                        return validationResult;
                    }

                    viewModel.PaymentInfo.BillingPlanInfo = contributionExisted.PaymentInfo.BillingPlanInfo;
                }
                else if (viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments.ToString())
                    && await HasSucceededPayment(contributionExisted.Id))
                {
                    return OperationResult.Failure("Sorry, it's not allowed to add split payments after the contribution has been approved");
                }

                if (contributionExisted.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
                {
                    var validationResult = await ValidateMonthlySessionSubscriptionOptions(viewModel, contributionExisted);

                    if (validationResult.Failed)
                    {
                        return validationResult;
                    }

                    viewModel.PaymentInfo.BillingPlanInfo = contributionExisted.PaymentInfo.BillingPlanInfo;
                }
                else if (viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription.ToString())
                    && await HasSucceededPayment(contributionExisted.Id))
                {
                    return OperationResult.Failure("Sorry, it's not allowed to add monthly session subscription after the contribution has been approved");
                }
            }
            if (viewModel.PaymentInfo.Cost.HasValue)
            {
                var grossAmount = _paymentSystemFeeService.CalculateGrossAmount(
                    viewModel.PaymentInfo.Cost.Value * _stripeService.SmallestCurrencyUnit,
                    viewModel.PaymentInfo.CoachPaysStripeFee, viewModel.UserId);

                if (grossAmount > _maxAllowedCostAmount)
                {
                    return OperationResult.Failure("Contribution cost must be less or" +
                                                   $" equal to {_maxAllowedCostAmount / _stripeService.SmallestCurrencyUnit} {contributionExisted.DefaultCurrency}");
                }
            }
            else if (viewModel is ContributionMembershipViewModel || viewModel is ContributionCommunityViewModel)
            {
                if (viewModel.PaymentInfo.MembershipInfo.Costs.Count != viewModel.PaymentInfo.PaymentOptions.Where(p => p != "Free").Count() &&
                    !isFreeOnly)
                {
                    return OperationResult.Failure("Contribution prices setup incorrectly");
                }
            }
            else if (!viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription.ToString()) &&
                        (!viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free.ToString())) &&
                        (viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free.ToString()) && viewModel.PaymentInfo.PaymentOptions?.Count() > 1))
            {
                return OperationResult.Failure("Contribution can be empty only for Monthly subscription");
            }
            viewModel.ConvertAllOwnZonedTimesToUtc(requesterUser.TimeZoneId);
            List<TimeRange> incomingBusyTimes = GetIncomingBusyTimes(viewModel);

            if (incomingBusyTimes.Any(x => incomingBusyTimes.Any(y => CheckTimeRangesCrossing(x, y))))
            {
                return OperationResult.Failure("Your availability time frame windows are overlapping. Please fix this on step 3");
            }

            var contribution = _mapper.Map<ContributionBase>(viewModel);
            var contributionBase = await _contributionRootService.GetOne(contribution.Id);
            contribution.UserId = contributionBase.UserId;

            await FillPodsForSessionContribution(viewModel);


            if (viewModel is SessionBasedContributionViewModel vm)
            {
                await UpdateZoomMeetingsInfo(contribution, contributionBase, requesterUser);
            }

            var isUpdateAllowed = viewModel.IsExistedSessionsModificationAllowed(
                contributionExisted,
                out var errorMessage,
                out var editedBookingsWithClientIds,
                out var deletedBookingsWithClientIds,
                out var deletedAttachments);

            if (!isUpdateAllowed)
            {
                return OperationResult.Failure(errorMessage);
            }
           
            var isCompletedSessionsChanged = contributionExisted.IsCompletedTimesChanged(_mapper.Map<ContributionBase>(viewModel), out errorMessage);

            if (isCompletedSessionsChanged)
            {
                return OperationResult.Failure(errorMessage);
            }

            foreach (var deletedAttachment in deletedAttachments)
            {
                await _fileManager.DeleteFileFromNonPublicStorageAsync(deletedAttachment.DocumentKeyWithExtension);
            }

            if (editedBookingsWithClientIds.Any())
            {
                if (contributionBase is SessionBasedContribution existedContribution && contribution is SessionBasedContribution updatedContribution)
                {
                    var eventDiff = existedContribution.GetEventsDiff($"{requesterUser.FirstName} {requesterUser.LastName}", updatedContribution, withPreRecorded: false);
                    if (IsLiveCourseCoachNotificationRequired(eventDiff))
                    {
                        await _notifictionService.SendSessionRescheduledNotification(editedBookingsWithClientIds, requesterUser.FirstName);
                    }
                }
            }

            if (deletedBookingsWithClientIds.Any())
            {
                if (contributionBase is SessionBasedContribution existedContribution && contribution is SessionBasedContribution updatedContribution)
                {
                    var eventDiff = existedContribution.GetEventsDiff($"{requesterUser.FirstName} {requesterUser.LastName}", updatedContribution, withPreRecorded: false);
                    if (eventDiff.CanceledEvents.Count > 0)
                    {
                        await _notifictionService.SendSessionDeletedNotification(deletedBookingsWithClientIds, requesterUser.FirstName);
                    }
                }
            }

             viewModel.AssignIdsToTimeRanges();
            
            var contributionStatus = ContributionStatuses.Revised;

            if (contributionExisted.PaymentType == PaymentTypes.Advance && !requesterUser.StandardAccountTransfersEnabled)
            {
                contributionStatus = ContributionStatuses.InSandbox;
            }
            else if (contributionExisted.PaymentType != PaymentTypes.Advance && !requesterUser.TransfersEnabled)
            {
                contributionStatus = ContributionStatuses.InSandbox;
            }

            if (contributionExisted.Chat != null)
            {
                contribution.Chat.PartnerChats = contributionExisted.Chat.PartnerChats;
            }

            contribution.Status = contributionExisted.Status == ContributionStatuses.Approved ? ContributionStatuses.Approved : contributionStatus;

            if (contributionExisted.Status == ContributionStatuses.Approved)
            {
                var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(contributorAccount.Id);
                if (contributionExisted.PaymentInfo.BillingPlanInfo is null)
                {
                    if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments))
                    {
                        if (contribution.PaymentType == PaymentTypes.Advance)
                        {
                            if (!requesterUser.IsStandardAccount)
                            {
                                return OperationResult.Failure("Advance payment is only for standard account users");
                            }
                            var createAdvanceBillingPlanResult = await AddContributionAssociatedSplitPaymentStripeProductPlanForAdvancePay(contribution, currentPaidTier.PaidTierOption, requesterUser.StripeStandardAccountId);

                            if (createAdvanceBillingPlanResult.Failed)
                            {
                                return createAdvanceBillingPlanResult;
                            }

                            contribution.PaymentInfo.BillingPlanInfo = createAdvanceBillingPlanResult.Payload;
                        }
                        else
                        {
                            var createBillingPlanResult = await AddContributionAssociatedSplitPaymentStripeProductPlan(contribution, currentPaidTier.PaidTierOption);

                            if (createBillingPlanResult.Failed)
                            {
                                return createBillingPlanResult;
                            }

                            contribution.PaymentInfo.BillingPlanInfo = createBillingPlanResult.Payload;
                        }
                    }

                    if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
                    {
                        string stripeStandardAccountId = string.Empty;
                        if (contribution.PaymentType == PaymentTypes.Advance && requesterUser.IsStandardAccount)
                        {
                            stripeStandardAccountId = requesterUser.StripeStandardAccountId;
                        }

                        // adavance payment is also handle in this function
                        var createBillingPlanResult = await AddContributionAssociatedMonthlySessionSubscriptionStripeProductPlan(contribution, currentPaidTier.PaidTierOption, stripeStandardAccountId, contributorUser.CountryId);

                        if (createBillingPlanResult.Failed)
                        {
                            return createBillingPlanResult;
                        }

                        contribution.PaymentInfo.BillingPlanInfo = createBillingPlanResult.Payload;
                        var contributionOneToOne = contribution as ContributionOneToOne;
                        contributionOneToOne.CoachStandardAccountId = stripeStandardAccountId;
                        await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);
                    }
                }

                if (contribution is ContributionMembership || contribution is ContributionCommunity)
                {
                    if ((contributionExisted.PaymentInfo.MembershipInfo.ProductBillingPlans is null ||
                        contributionExisted.PaymentInfo.MembershipInfo.ProductBillingPlans.Count == 0) &&
                        !isFreeOnly)
                    {
                        string stripeStandardAccountId = string.Empty;
                        if (contribution.PaymentType == PaymentTypes.Advance && requesterUser.IsStandardAccount)
                        {
                            stripeStandardAccountId = requesterUser.StripeStandardAccountId;
                        }
                        //adavance payment is also handle in this function

                        var createBillingPlansResult =
                            await AddContributionAssociatedSessionBaseStripeProductPlan((SessionBasedContribution)contribution, currentPaidTier.PaidTierOption, stripeStandardAccountId);

                        if (createBillingPlansResult.Failed)
                        {
                            return createBillingPlansResult;
                        }

                        contribution.PaymentInfo.MembershipInfo.ProductBillingPlans = createBillingPlansResult.Payload;
                    }
                }
            }

            await NotifyAboutClassesChanges(requesterUser, contribution, contributionBase);
            var updatedContributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            if (updatedContributionBase is SessionBasedContribution updatedCourse && contributionBase is SessionBasedContribution existedCourse)
            {
                var eventDiff = existedCourse.GetEventsDiff($"{requesterUser.FirstName} {requesterUser.LastName}", updatedCourse);
                var selfPacedeventDiff = existedCourse.GetEventsDiff($"{requesterUser.FirstName} {requesterUser.LastName}", updatedCourse, true);

                var bookSessionTimeModels = new List<BookSessionTimeViewModel>();
                var userIds = updatedCourse.Sessions.SelectMany(x => x.SessionTimes).SelectMany(x => x.ParticipantsIds).Distinct();
                var allUserIds = new List<string>();
                var allPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(m => m.ContributionId == updatedCourse.Id);
                if (allPurchases.Count() > 0)
                {
                    var allUsers = _mapper.Map<List<PurchaseViewModel>>(allPurchases).Select(m => m.ClientId);
                    allUserIds = userIds.Concat(allUsers).Distinct().ToList();
                }
                else
                {
                    allUserIds = userIds.ToList();
                }
               
                var users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => allUserIds.Contains(u.Id));

                #region Send Push Notification
                try
                {
                    var participants = await GetParticipantsVmsAsync(contribution.Id);
                    await _fcmService.SendSessionPushNotification(selfPacedeventDiff, updatedCourse.Id, requesterUser.Id, participants.Select(x => x.Id).ToList());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during send push notifiaction on updated calendar events");

                }
                #endregion

                foreach (var session in updatedCourse.Sessions.Where(m => !m.IsPrerecorded && m.SessionTimes.Count == 1))
                {
                    foreach (var sessionTime in session.SessionTimes)
                    {
                        if (session.SessionTimes.Count == 1)
                        {
                            var baseSessionTime = existedCourse.Sessions.FirstOrDefault(x => x.Id == session.Id)?.SessionTimes?.FirstOrDefault(x => x.Id == sessionTime.Id);
                            if (baseSessionTime == null)
                            {
                                bookSessionTimeModels.Add(new BookSessionTimeViewModel
                                {
                                    ContributionId = contribution.Id,
                                    SessionId = session.Id,
                                    SessionTimeId = sessionTime.Id
                                });
                            }
                        }
                       
                    }
                }
                foreach (var accountId in users.Select(x => x.AccountId))
                {
                    if (bookSessionTimeModels.Any())
                         _contributionBookingService.BookSessionTimeAsync(bookSessionTimeModels, accountId);
                    
                }
                if (eventDiff.CanceledEvents.Any())
                {
                    foreach (SessionTimeToSession sessionTimeToSession in eventDiff.CanceledEvents)
                    {
                        string nylasEventId = "";
                        var revokedbooksession = new BookSessionTimeViewModel
                        {
                            ContributionId = existedCourse.Id,
                            SessionId = sessionTimeToSession.Session.Id,
                            SessionTimeId = sessionTimeToSession.SessionTime.Id
                        };
                        if (sessionTimeToSession.SessionTime.EventInfos.Any())
                        {
                            nylasEventId = sessionTimeToSession.SessionTime.EventInfos.FirstOrDefault().CalendarEventID;
                        }
                        //await _contributionBookingService.RevokeBookingOfSessionTimeAsync(revokedbooksession, accountId, existedCourse);
                        // await _notifictionService.DeleteCalendarEventForSessionBase(updatedCourse, sessionTimeToSession.SessionTime.Id, accountId);
                        bool response = await _notifictionService.DeleteCalendarEventForSessionBase(updatedCourse, revokedbooksession.SessionTimeId, requesterUser.AccountId, nylasEventId);
                    }
                }

                var existedCourseParticipantsIds = existedCourse.Sessions.Where(s => !s.IsPrerecorded)?.SelectMany(m => m.SessionTimes.Where(s => !s.IsCompleted))?
                    .Select(m => m.ParticipantsIds).ToList()?.OrderByDescending(m => m.Count).FirstOrDefault();
                var updatedCourseParticipantsIds = updatedCourse.Sessions.Where(s => !s.IsPrerecorded)?.SelectMany(m => m.SessionTimes.Where(s => !s.IsCompleted))?
                    .Select(m => m.ParticipantsIds).ToList()?.OrderByDescending(m => m.Count).FirstOrDefault();

                if (existedCourseParticipantsIds?.Count() > updatedCourseParticipantsIds?.Count()) //autobook sessions for the user who purchase the contribution while updating it
                {
                    List<string> participantIdsTobeAdded = new List<string>();
                    foreach (var participantId in existedCourseParticipantsIds)
                    {
                        if (!updatedCourseParticipantsIds.Contains(participantId))
                            participantIdsTobeAdded.Add(participantId);
                    }
                    foreach (var session in updatedCourse.Sessions.Where(session => !session.IsPrerecorded && session.SessionTimes.Count == 1))
                    {
                        var sessionTime = session.SessionTimes.FirstOrDefault();
                        if (sessionTime is not null && !sessionTime.IsCompleted)
                        {
                            var remainingSpaceForParticipant = (int)session.MaxParticipantsNumber - sessionTime.ParticipantsIds.Count();
                            if (remainingSpaceForParticipant >= participantIdsTobeAdded.Count()) //enough space to add all participants in the list
                                sessionTime.ParticipantsIds = sessionTime.ParticipantsIds?.Concat(participantIdsTobeAdded).ToList();
                            else if (remainingSpaceForParticipant > 0)
                                sessionTime.ParticipantsIds = sessionTime.ParticipantsIds?.Concat(participantIdsTobeAdded.Take(remainingSpaceForParticipant)).ToList();
                        }
                    }
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(existedCourse.Id, updatedCourse);
                }
            }
            updatedContributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == updatedContributionBase.Id);


            await UpdateChat(contributionExisted, contribution, updatedContributionBase);

            var updatedViewModel = _mapper.Map<ContributionBaseViewModel>(updatedContributionBase);

            updatedViewModel.ConvertAllOwnUtcTimesToZoned(requesterUser.TimeZoneId);

            var contributionPartners = await GetContributionPartnersAsync(contribution.Id);
            if (contributionPartners.Succeeded)
            {
                updatedViewModel.ContributionPartners = contributionPartners.Payload;
            }

            updatedViewModel.Type = viewModel.Type;
            updatedViewModel.AvailableCurrencies = GetCurrenciesForContribution(requesterUser.CountryId).Result.ToList();

            if (updatedContributionBase.Status == ContributionStatuses.InReview || updatedContributionBase.Status == ContributionStatuses.Revised)
            {
                await _notifictionService.SendEmailAboutInReviewToAdmins(updatedContributionBase);
            }

            if (updatedContributionBase.Status == ContributionStatuses.Approved)
            {
                ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
                ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                {
                    CohereAccountId = requesterUser.AccountId,
                    ContributionStatus = EnumHelper<ContributionStatus>.GetDisplayValue(ContributionStatus.Draft),

                };
                _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
            }


            return OperationResult.Success("Success", updatedViewModel);

            async Task NotifyAboutClassesChanges(User requesterUser, ContributionBase contribution, ContributionBase contributionBase)
            {
                try
                {
                    if (contributionBase is SessionBasedContribution existedCourse && contribution is SessionBasedContribution updatedCourse)
                    {
                        var eventDiff = existedCourse.GetEventsDiff($"{requesterUser.FirstName} {requesterUser.LastName}", updatedCourse);

                        #region debugMessages

                        _logger.LogDebug($"updated events count: {eventDiff.UpdatedEvents.Count}");
                        _logger.LogDebug($"created events count: {eventDiff.CreatedEvents.Count}");
                        _logger.LogDebug($"canceled events count: {eventDiff.CanceledEvents.Count}");
                        _logger.LogDebug($"not modified events count: {eventDiff.NotModifiedEvents.Count}");

                        #endregion

                        //Nylas invites remove/Add accordingly
                        NylasAccount NylasAccount = null;
                        if (!string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                            NylasAccount = await _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == contributorUser.AccountId && n.EmailAddress.ToLower() == updatedCourse.ExternalCalendarEmail.ToLower());
                        // var sessionList = updatedCourse.Sessions.SelectMany(x => x.SessionTimes).Where(x=>x.EventInfo.Count>0).ToList();
                        List<string> distinctParticipants = new List<string>();
                        if (NylasAccount != null && !string.IsNullOrEmpty(updatedCourse.ExternalCalendarEmail))
                        {

                            if (eventDiff.UpdatedEvents.Any())
                            {
                                //var userIds = updatedCourse.Sessions.SelectMany(x => x.SessionTimes).SelectMany(x => x.ParticipantsIds).Distinct();
                                //var users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => userIds.Contains(u.Id));
                                var locationUrl = updatedCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedCourse.Id));
                                var allParticipantsUserIds = new Dictionary<string, bool>();

                                foreach (SessionTimeToSession item in eventDiff.UpdatedEvents)
                                {
                                    //EventInfo eventInf = item.SessionTime.EventInfos.Where(x => x.ParticipantId == participant).FirstOrDefault();
                                    EventInfo eventInf = item.SessionTime.EventInfos.FirstOrDefault();
                                    var ids = item.SessionTime.ParticipantsIds.Distinct().ToList();
                                    if (eventInf != null)
                                        if (eventInf.CalendarId == NylasAccount.CalendarId)
                                        {
                                            CalendarEvent calevent = _mapper.Map<CalendarEvent>(item);
                                            calevent.Location = locationUrl;
                                            calevent.Description = contribution.CustomInvitationBody;
                                            NylasEventCreation eventResponse = await _notifictionService.CreateorUpdateCalendarEventForSessionBase(calevent, ids.ToList(), NylasAccount, item, true, eventInf.CalendarEventID);


                                            //eventInf.CalendarEventID = eventResponse.id;
                                            //eventInf.CalendarId = eventResponse.calendar_id;
                                            //eventInf.NylasAccountId = eventResponse.account_id;
                                            //eventInf.AccessToken = NylasAccount.AccessToken;
                                            //eventInf.ParticipantId = participant;

                                        }

                                }

                                // await _notifictionService.SendLiveCourseWasUpdatedNotificationAsync(updatedCourse.Title, allParticipantsUserIds, locationUrl, eventDiff, updatedCourse.UserId, updatedCourse.Id);

                            }

                        }
                        else if (IsLiveCourseCoachNotificationRequired(eventDiff))
                        {
                            var locationUrl = updatedCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedCourse.Id));
                            var coachAndPartnersUserIds = new Dictionary<string, bool>() { { contributionBase.UserId, true } };
                            foreach (var partner in contributionBase.Partners.Where(e => e.IsAssigned))
                            {
                                coachAndPartnersUserIds.Add(partner.UserId, false);
                            }

                            await _notifictionService.SendLiveCourseWasUpdatedNotificationAsync(updatedCourse.Title, coachAndPartnersUserIds, locationUrl, eventDiff, updatedCourse.UserId, updatedCourse.Id);
                        }
                    }

                    if (contributionBase is ContributionOneToOne existedOneToOne && contribution is ContributionOneToOne updatedOneToOne)
                    {
                        if (updatedOneToOne.LiveVideoServiceProvider != existedOneToOne.LiveVideoServiceProvider)
                        {
                            await UpdateCoachCalendarEventsLocation(updatedOneToOne);

                            await UpdateOneToOneClientsCalendarEventsLocation(updatedOneToOne);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during updating calendar events");
                }
            }

            async Task UpdateChat(ContributionBase contributionExisted, ContributionBase contribution, ContributionBase updatedContributionBase)
            {
                if (updatedContributionBase is SessionBasedContribution
                    && HasChat(contributionExisted)
                    && IsContributionTitleOrImageChanged(contributionExisted, contribution))
                {
                    await _chatService.UpdateChatForContribution(updatedContributionBase);
                }
            }
        }

        public async Task UserViewedRecording(UserViewedRecordingViewModel model)
        {
            var contribution = await _contributionRootService.GetOne(model.ContributionId);
            if (contribution is SessionBasedContribution course)
            {
                var sessionTime = course.Sessions.SelectMany(x => x.SessionTimes).FirstOrDefault(x => x.Id == model.SessionTimeId);
                if (sessionTime != null && !sessionTime.UsersWhoViewedRecording.Contains(model.UserId))
                {
                    sessionTime.UsersWhoViewedRecording.Add(model.UserId);
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(model.ContributionId, contribution);
                }
            }
        }

        public async Task<IEnumerable<ContributionBaseViewModel>> GetClientContributionByType(string accountId, string type)
        {
            if (!ContributionTypes.Contains(type))
            {
                throw new Exception("not supported type");
            }

            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == client.Id);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).Where(p => p.ContributionType == type).ToList();
            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
            var allAccessibleByClient = purchaseVms.Where(p =>
                p.HasAccessToContribution
                && p.ContributionType == type)
                .ToList();

            var contributionIds = allAccessibleByClient.Select(c => c.ContributionId).Distinct().ToList();

            var result = new List<ContributionBaseViewModel>();

            foreach (var contributionId in contributionIds)
            {
                result.Add(await GetClientContributionByIdAsync(contributionId, client.AccountId));
            }

            return result;
        }

        public async Task<List<ParticipantViewModel>> GetParticipantsVmsAsync(string contributionId, User user = null)
        {
            var allPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributionId == contributionId);
            var allPurchasesVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(allPurchases).ToList();
            foreach (var participantPurchaseVm in allPurchasesVms)
            {
                if ((user == null || participantPurchaseVm.ClientId == user.Id))
                {
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
                    var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
                    participantPurchaseVm.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
                }
            }

            var participantsWithAccess = allPurchasesVms
                .Where(p => p.HasAccessToContribution)
                .ToList();

            var trialParticipants = participantsWithAccess
                .Where(e => e.RecentPaymentOption == PaymentOptions.Trial)
                .Select(e => e.ClientId)
                .ToHashSet();

            var participantsIds = participantsWithAccess
                .Select(p => p.ClientId)
                .ToList();

            if (participantsIds.Count <= 0)
            {
                return new List<ParticipantViewModel>();
            }

            var participants = await _unitOfWork.GetRepositoryAsync<User>().Get(u => participantsIds.Contains(u.Id));
            var participantsDict = _mapper.Map<List<ParticipantViewModel>>(participants).ToDictionary(e => e.Id);

            var addedByAccessCode = participantsDict.Keys.Intersect(trialParticipants);

            foreach (var participantId in addedByAccessCode)
            {
                participantsDict[participantId].IsAddedByAccessCode = true;
            }

            return participantsDict.Values.ToList();
        }

        public async Task<List<ParticipantInfo>> GetSessionsTimeParticipantsInfoAsync(List<string> participantsIds)
        {
            var participants = await _unitOfWork.GetRepositoryAsync<User>().Get(u => participantsIds.Contains(u.Id));
            var participantsDict = _mapper.Map<List<ParticipantInfo>>(participants).ToDictionary(e => e.ParticipantId);

            return participantsDict.Values.ToList();
        }
        public async Task<JourneyClassesAllViewModelUpdated> GetBoughtByUserIdUpdated(string userId)
        {
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == userId);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();

            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);

            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));


            var purchaseSucceededVmsList = purchaseVms.Where(p => p.HasAccessToContribution).ToList();

            var contributionIds = purchaseSucceededVmsList.Select(c => c.ContributionId);

            var boughtContributions = await _contributionRootService.Get(c => contributionIds.Contains(c.Id));
            var boughtContributionsList = boughtContributions.ToList();

            var boughtContributionsVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(boughtContributionsList).ToList();

            var boughtContributionsAuthorIds = boughtContributionsList.Select(c => c.UserId);
            var authors = await _unitOfWork.GetRepositoryAsync<User>().Get(u => boughtContributionsAuthorIds.Contains(u.Id));
            var authorsUserDict = authors.ToDictionary(e => e.Id);

            var allClassesInfos = new JourneyClassesInfosAllUpdated();

            var closestClassForBanner = new ClosestClassForBannerViewModel();
            var initialValueMinutesLeft = int.MaxValue;
            closestClassForBanner.MinutesLeft = initialValueMinutesLeft;

            foreach (var boughtContributionVm in boughtContributionsVms)
            {
                // Commenting.
                await FillPodsForSessionContribution(boughtContributionVm);
                var IsAccessRevokedByCoach = purchases.First(p => p.ContributionId == boughtContributionVm.Id).Payments.LastOrDefault().IsAccessRevokedByCoach;
                string clientTimeZoneId = client?.TimeZoneId;
                if (boughtContributionVm is SessionBasedContributionViewModel sessionBasedModel)
                {
                    var multipleSessionTimes = sessionBasedModel.Sessions.Where(m => m.SessionTimes.Count() > 1 && !m.IsPrerecorded);
                    var completedMultipleSessionTimes = multipleSessionTimes.SelectMany(m => m.SessionTimes.Where(p => p.IsCompleted)).Count();
                    var incompletedMultipleSessionTimes = multipleSessionTimes.SelectMany(m => m.SessionTimes.Where(p => !p.IsCompleted && p.StartTime < DateTime.UtcNow)).Count();
                    var upcomingMultipleSessionTimes = multipleSessionTimes.SelectMany(m => m.SessionTimes.Where(p => !p.IsCompleted && p.StartTime >= DateTime.UtcNow)).Count();


                    var allLiveSessions = sessionBasedModel.Sessions?.Where(m => !m.IsPrerecorded).SelectMany(x => x.SessionTimes);
                    var allCompletedLiveSessions = allLiveSessions?.Where(m => m.IsCompleted);
                    var inCompletedLiveSessionCount = allLiveSessions?.Where(m => m.StartTime < DateTime.UtcNow && !m.IsCompleted).Count();
                    var upComingLiveSessionCount = allLiveSessions?.Where(m => m.StartTime >= DateTime.UtcNow && !m.IsCompleted).Count();

                    var allSelfPacedSessions = sessionBasedModel.Sessions?.Where(m => m.IsPrerecorded).SelectMany(x => x.SessionTimes);
                    var allCompletedSelfPacedSessions = allSelfPacedSessions?.Where(p => p.CompletedSelfPacedParticipantIds.Contains(userId));
                    var upcomingselfpacedSessionCount = allSelfPacedSessions?.Where(m => !m.CompletedSelfPacedParticipantIds.Contains(userId)).Count();

                    var allLiveSessionsWithSingleSessionTime = sessionBasedModel.Sessions?.Where(m => !m.IsPrerecorded && m.SessionTimes.Count() == 1).SelectMany(x => x.SessionTimes?.Where(m => !m.IsCompleted));
                    var allLiveSessionsWithSingleSessionTimeParticpants = allLiveSessionsWithSingleSessionTime.Select(m => m.ParticipantsIds);

                    boughtContributionVm.PercentageCompleted = GetCompletedPercentageOfContribution(sessionBasedModel, false, userId);
                    
                    var dateTimeForUpComing = allLiveSessions?.OrderBy(m => m.StartTime).FirstOrDefault(m => !m.IsCompleted && m.StartTime >= DateTime.UtcNow)?.StartTime;
                    var dateTimeForInComplete = allLiveSessions?.OrderByDescending(m => m.StartTime).FirstOrDefault(m => m.StartTime <= DateTime.UtcNow)?.StartTime;
                    var dateTimeForPast =allCompletedLiveSessions?.OrderByDescending(m => m.StartTime).FirstOrDefault()?.StartTime;
                    
                    var latestSessionDateTimeForInComplete = dateTimeForInComplete.HasValue ?  DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForInComplete), clientTimeZoneId) : dateTimeForInComplete;
                    var latestSessionDateTimeForUpComing = dateTimeForUpComing.HasValue ? DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForUpComing), clientTimeZoneId) : dateTimeForUpComing;
                    var pastSessionDateTime = dateTimeForPast.HasValue ? DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForPast), clientTimeZoneId): dateTimeForPast;

                    var bookedSessionsParticpants = allLiveSessions?.SelectMany(m => m.ParticipantsIds);
                    var bookableMultipleSessionsTime = multipleSessionTimes?.Select(m => m.SessionTimes?.Where(m => !m.IsCompleted));

                    // Past sessions
                    // When one session in contribution is completed move it to past.
                    if ((allCompletedLiveSessions.Any() || allCompletedSelfPacedSessions.Any()) && completedMultipleSessionTimes == incompletedMultipleSessionTimes)
                    {
                        allClassesInfos.Past.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, allCompletedSelfPacedSessions.Any() ? true : false, null, null, pastSessionDateTime, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // Modules
                    // Contribution with Self-paced sessions only.
                    if (upcomingselfpacedSessionCount > 0)
                    {
                        allClassesInfos.Modules.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, true, null, null, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    if ((allCompletedLiveSessions?.Count() != allLiveSessions?.Count()) || (allCompletedSelfPacedSessions?.Count() != allSelfPacedSessions?.Count()))
                    {
                        // Multiple session times and some of the sesion times are booked and some are not so move to bookable.
                        if (multipleSessionTimes.Count() > 0 && bookableMultipleSessionsTime.Any(m => !m.Any(p => p.ParticipantsIds.Contains(client?.Id))))
                        {
                            allClassesInfos.Bookable.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, latestSessionDateTimeForUpComing, latestSessionDateTimeForInComplete, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                        }
                        // Single session and some of the sesions are booked and some are not so move to bookable.
                        // case handled when user un-book session by it self.
                        else if (allLiveSessionsWithSingleSessionTime.Count() > 0 && allLiveSessionsWithSingleSessionTimeParticpants.Any(m => !m.Any(p => p == client?.Id)))
                        {
                            allClassesInfos.Bookable.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, latestSessionDateTimeForUpComing, latestSessionDateTimeForInComplete, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                        }
                        // Datetime is past for session and session is booked or not move it to InCompleted.
                        if (inCompletedLiveSessionCount > 0 && upComingLiveSessionCount > 0 || incompletedMultipleSessionTimes > 0)
                        {
                            allClassesInfos.InCompleted.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, latestSessionDateTimeForUpComing, latestSessionDateTimeForInComplete, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                        }
                        // No upcoming session for contribution but have session whose datetime has past.
                        else if (inCompletedLiveSessionCount > 0 && upComingLiveSessionCount == 0)
                        {
                            allClassesInfos.InCompleted.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, latestSessionDateTimeForUpComing, latestSessionDateTimeForInComplete, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                        }
                        // Upcoming sessions will go here.
                        if (upComingLiveSessionCount > 0 && bookedSessionsParticpants.Contains(client?.Id))
                        {
                            allClassesInfos.Upcoming.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, latestSessionDateTimeForUpComing, latestSessionDateTimeForInComplete, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                        }
                    }

                }
                else if (boughtContributionVm is ContributionOneToOneViewModel oneToOneBasedSession)
                {
                    int notBookedClassesFromPackagesCount = 0;
                    var singlePurchase = purchaseVms?.FirstOrDefault(m => m.ClientId == userId && m.ContributionId == boughtContributionVm.Id);

                    if (oneToOneBasedSession.PackagePurchases.Any())
                    {
                        notBookedClassesFromPackagesCount = oneToOneBasedSession.PackagePurchases
                            .Where(pp => pp.UserId == userId && !pp.IsCompleted && pp.IsConfirmed)
                            .Sum(p => p.FreeSessionNumbers);
                    }

                    oneToOneBasedSession.PercentageCompleted = singlePurchase == null ? 0 : OnetoOneProgressBarData(oneToOneBasedSession, userId, singlePurchase);

                    var allCompletedSessions = oneToOneBasedSession.AvailabilityTimes?.SelectMany(x => x.BookedTimes?.Where(m => m.IsCompleted && m.ParticipantId == userId));
                    var upcomingSessions = oneToOneBasedSession.AvailabilityTimes?.SelectMany(x => x.BookedTimes?.Where(m => !m.IsCompleted && m.ParticipantId == userId));
                    
                    var upcomingSessionCount = upcomingSessions.Count();
                    var dateTimeForUpComing = upcomingSessionCount > 0 ? upcomingSessions.OrderByDescending(m => m.StartTime).FirstOrDefault()?.StartTime : null;
                    var dateTimeForInCompleted = upcomingSessionCount > 0 ? upcomingSessions.OrderByDescending(m => m.StartTime).FirstOrDefault()?.StartTime : null;

                    bool bookedPerSessionWithDateTimePast = DateTime.UtcNow >  dateTimeForUpComing && !oneToOneBasedSession.PackagePurchases.Any() ? true : false;
                    bool bookedSessionPkgWithDateTimePast = DateTime.UtcNow > dateTimeForUpComing && oneToOneBasedSession.PackagePurchases.Any() ? true : false;
                    
                    var upcmoingSessionDateTime = dateTimeForUpComing.HasValue ? DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForUpComing), clientTimeZoneId) : dateTimeForUpComing;

                   var inCompletedSessionDateTime = dateTimeForInCompleted.HasValue ? DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForInCompleted), clientTimeZoneId) : dateTimeForInCompleted;

                    var pastSessions = oneToOneBasedSession.AvailabilityTimes?.SelectMany(x => x.BookedTimes.Where(m => m.ParticipantId == userId && m.IsCompleted));
                    var dateTimeForPast = pastSessions.Count() > 0 ? pastSessions.OrderByDescending(m => m.StartTime)?.FirstOrDefault().StartTime : null;
                    var pastSessionDateTime = dateTimeForPast.HasValue ? DateTimeHelper.GetZonedDateTimeFromUtc(Convert.ToDateTime(dateTimeForPast), clientTimeZoneId) : dateTimeForPast;

                    // move session pkg only to past.
                    if (allCompletedSessions.Any() && oneToOneBasedSession.AvailabilityTimes.Count > 0 && notBookedClassesFromPackagesCount == 0)
                    {
                        allClassesInfos.Past.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, null, null, pastSessionDateTime, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // move per session only to past.
                    else if (allCompletedSessions.Any() && oneToOneBasedSession.AvailabilityTimes.Count > 0 && !oneToOneBasedSession.PackagePurchases.Any())
                    {
                        allClassesInfos.Past.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, null, null, pastSessionDateTime, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // for session pkg some sesions are booked and some are not so move it to bookable.
                    if (notBookedClassesFromPackagesCount > 0)
                    {
                        allClassesInfos.Bookable.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, null, null, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // upcoming for session pkg
                    if (upcomingSessionCount > 0 && !allCompletedSessions.Any() && !bookedPerSessionWithDateTimePast && !bookedSessionPkgWithDateTimePast)
                    {
                        allClassesInfos.Upcoming.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, upcmoingSessionDateTime, null, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // upcoming for session pkg
                    else if (upcomingSessionCount > 0 && oneToOneBasedSession.PackagePurchases.Any() && !bookedSessionPkgWithDateTimePast)
                    {
                        allClassesInfos.Upcoming.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, upcmoingSessionDateTime, null, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // upcoming for per session
                    else if (allCompletedSessions.Count() == 0 && !oneToOneBasedSession.PackagePurchases.Any() && !bookedPerSessionWithDateTimePast)
                    {
                        allClassesInfos.Upcoming.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, upcmoingSessionDateTime, null, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // move to InCompleted for per session when datetime is past.
                    else if (bookedPerSessionWithDateTimePast)
                    {
                        allClassesInfos.InCompleted.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, null, inCompletedSessionDateTime, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                    // move to InCompleted for session pkg when datetime is past.
                    else if (bookedSessionPkgWithDateTimePast)
                    {
                        allClassesInfos.InCompleted.Add(await MapContributionForGetAllBoughtByUserId(boughtContributionVm, userId, false, null, inCompletedSessionDateTime, null, boughtContributionVm.PercentageCompleted, IsAccessRevokedByCoach, client.TimeZoneId));
                    }
                }

                if (authorsUserDict.ContainsKey(boughtContributionVm.UserId))
                {
                    var coachUserTimeZoneId = authorsUserDict[boughtContributionVm.UserId].TimeZoneId;
                    var currentClosestClassForBanner = boughtContributionVm.GetClosestClientClassForBanner(userId, coachUserTimeZoneId);
                    if (currentClosestClassForBanner != null && currentClosestClassForBanner.MinutesLeft < closestClassForBanner.MinutesLeft)
                    {
                        closestClassForBanner = currentClosestClassForBanner;
                    }
                }
            }

            var isClosestTimeIsInitial = closestClassForBanner.MinutesLeft == initialValueMinutesLeft;
            if (!isClosestTimeIsInitial)
            {
                if (closestClassForBanner.ContributionType == nameof(ContributionOneToOne))
                {
                    var authorUser = authorsUserDict[closestClassForBanner.AuthorUserId];
                    closestClassForBanner.Title = $"{authorUser.FirstName} {authorUser.LastName}";
                }
            }
            var failedSubscription = await ListClientIncompleteSubscription(client.AccountId);

            var journeyClassesAllVm = new JourneyClassesAllViewModelUpdated
            {
                ClosestClassForBanner = !isClosestTimeIsInitial ? closestClassForBanner : null,
                Upcoming = allClassesInfos.Upcoming,
                UpcomingTotalCount = allClassesInfos.Upcoming.Count,
                Modules = allClassesInfos.Modules,
                InCompletetd = allClassesInfos.InCompleted.DistinctBy(m => m.ContributionId).ToList(),
                Bookable = allClassesInfos.Bookable,
                BookableTotalCount = allClassesInfos.Bookable.Count,
                Past = allClassesInfos.Past,
                PastTotalCount = allClassesInfos.Past.Count,
                ClientDeclinedSubscriptions = failedSubscription,
            };

            return journeyClassesAllVm;
        }
        private async Task<JourneyClassInfo> MapContributionForGetAllBoughtByUserId(ContributionBaseViewModel boughtContributionVm, string userId, bool isPrerecorded, DateTime? upcomingDateTime,
            DateTime? inCompleteDateTime, DateTime? pastSessionDateTime, int percentageCompleted, bool isAccessRevokedByCoach, string clientTimeZoneId)
        {
            var model = new JourneyClassInfo();

            var contributionAuthor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(m => m.Id == boughtContributionVm.UserId);
            var clientTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == clientTimeZoneId);

            model.ContributionId = boughtContributionVm.Id;
            model.ContributionTitle = boughtContributionVm.Title;
            model.PreviewContentUrls = boughtContributionVm.PreviewContentUrls;
            model.TimeZoneShortForm = clientTimeZone?.ShortName;
            model.AuthorName = $"{contributionAuthor.FirstName} {contributionAuthor.LastName}";
            model.UpComingSesionTime = upcomingDateTime;
            model.InCompletedSesionTime = inCompleteDateTime;
            model.PastSesionTime = pastSessionDateTime;
            model.Type = boughtContributionVm.Type;
            model.AuthorUserId = contributionAuthor.Id;
            model.AuthorAvatarUrl = contributionAuthor.AvatarUrl;
            model.IsPrerecorded = isPrerecorded;
            model.PercentageCompleted = percentageCompleted;
            model.GroupSessions = boughtContributionVm is SessionBasedContributionViewModel sessionBased ? sessionBased?.Sessions : new List<Session>();
            model.OneToOneSessions = boughtContributionVm is ContributionOneToOneViewModel oneToOneBasedSession ? oneToOneBasedSession?.AvailabilityTimes : new List<AvailabilityTime>();
            model.IsAccessRevokedByCoach = isAccessRevokedByCoach;

            return model;
        }
        private int OnetoOneProgressBarData(ContributionOneToOneViewModel onetoOneBasedContrib, string requesterUserId, PurchaseViewModel purchaseVm)
        {
            int totalSessionsCount = 0;
            double completedPercentageInDecimal = 0.0;
            int percentageCompleted = 0;
            if (onetoOneBasedContrib.PackagePurchases.Count > 0 && purchaseVm?.Payments?.FirstOrDefault()?.PaymentOption == PaymentOptions.SessionsPackage)
            {
                var latestPurchasedPkg = onetoOneBasedContrib.PackagePurchases.Where(m => m.UserId == requesterUserId);
                var sessionIds = latestPurchasedPkg?.LastOrDefault()?.AvailabilityTimeIdBookedTimeIdPairs?.Select(m => m.Key).ToList();
                if (latestPurchasedPkg.Count() > 0 && sessionIds.Count > 0 && onetoOneBasedContrib.AvailabilityTimes?.Count > 0)
                {
                    totalSessionsCount = latestPurchasedPkg.LastOrDefault().SessionNumbers;
                    foreach (var session in onetoOneBasedContrib.AvailabilityTimes.Where(m => sessionIds.Contains(m.Id)))
                    {
                        completedPercentageInDecimal += session.BookedTimes.Count(st => st.IsCompleted);
                    }
                }
            }
            percentageCompleted = totalSessionsCount > 0 ? Convert.ToInt32(Math.Ceiling(100 * completedPercentageInDecimal / totalSessionsCount)) : 0;
            return percentageCompleted;
        }
        public async Task<JourneyClassesAllViewModel> GetForClientJourneyAsync(string userId)
        {
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var clientTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == client.TimeZoneId);

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == userId);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
            var purchaseSucceededVmsList = purchaseVms.Where(p => p.HasAccessToContribution).ToList();

            var contributionIds = purchaseSucceededVmsList.Select(c => c.ContributionId);

            var boughtContributions = await _contributionRootService.Get(c => contributionIds.Contains(c.Id));
            var boughtContributionsList = boughtContributions.ToList();

            var boughtContributionsVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(boughtContributionsList).ToList();

            var boughtContributionsAuthorIds = boughtContributionsList.Select(c => c.UserId);
            var authors = await _unitOfWork.GetRepositoryAsync<User>().Get(u => boughtContributionsAuthorIds.Contains(u.Id));
            var authorsUserDict = authors.ToDictionary(e => e.Id);

            var allClassesInfos = new List<JourneyClassesInfosAll>();

            var closestClassForBanner = new ClosestClassForBannerViewModel();
            var initialValueMinutesLeft = int.MaxValue;
            closestClassForBanner.MinutesLeft = initialValueMinutesLeft;

            foreach (var boughtContributionVm in boughtContributionsVms)
            {
                await FillPodsForSessionContribution(boughtContributionVm);
                if (boughtContributionVm is SessionBasedContributionViewModel sessionBased)
                {
                    boughtContributionVm.PercentageCompleted = GetCompletedPercentageOfContribution(sessionBased, false, userId);
                }
                

                allClassesInfos.Add(boughtContributionVm.GetClassesInfosForParticipant(userId, client?.TimeZoneId, clientTimeZone?.ShortName));

                if (authorsUserDict.ContainsKey(boughtContributionVm.UserId))
                {
                    var coachUserTimeZoneId = authorsUserDict[boughtContributionVm.UserId].TimeZoneId;
                    var currentClosestClassForBanner = boughtContributionVm.GetClosestClientClassForBanner(userId, coachUserTimeZoneId);
                    if (currentClosestClassForBanner != null && currentClosestClassForBanner.MinutesLeft < closestClassForBanner.MinutesLeft)
                    {
                        closestClassForBanner = currentClosestClassForBanner;
                    }
                }
            }

            var isClosestTimeIsInitial = closestClassForBanner.MinutesLeft == initialValueMinutesLeft;
            if (!isClosestTimeIsInitial)
            {
                if (closestClassForBanner.ContributionType == nameof(ContributionOneToOne))
                {
                    var authorUser = authorsUserDict[closestClassForBanner.AuthorUserId];
                    closestClassForBanner.Title = $"{authorUser.FirstName} {authorUser.LastName}";
                }
            }

            var pastClassesInfos = allClassesInfos.SelectMany(classInfo => classInfo.Past)
                .Concat(allClassesInfos.SelectMany(classInfo => classInfo.OtherCompleted)).ToList();
            var upcomingClassesInfos = allClassesInfos.SelectMany(classInfo => classInfo.Upcoming).ToList();
            var notBookedClassesInfos = allClassesInfos.SelectMany(classInfo => classInfo.NotBooked).ToList();
            var otherUncompletedClassesInfos = allClassesInfos.SelectMany(classInfo => classInfo.OtherUncompleted).ToList();

            var pastClassesVms = MapJourneyClassesInfosToViewModels(pastClassesInfos, authorsUserDict, purchaseVms, client);
            var pastThisWeekClasses = pastClassesVms
                .Where(c => c.SessionTime >= DateTimeHelper.StartOfWeek).ToList();

            var pastThisMonthClasses = pastClassesVms
                .Where(c => c.SessionTime >= DateTimeHelper.StartOfMonth)
                .Except(pastThisWeekClasses).ToList();

            var pastLastMonthClasses = pastClassesVms
                .Where(c => c.SessionTime >= DateTimeHelper.StartOfLastMonth)
                .Except(pastThisMonthClasses)
                .Except(pastThisWeekClasses).ToList();

            var pastThisYearClasses = pastClassesVms
                .Where(c => c.SessionTime >= DateTimeHelper.StartOfYear)
                .Except(pastLastMonthClasses)
                .Except(pastThisMonthClasses)
                .Except(pastThisWeekClasses);

            var pastPriorYearsClasses = pastClassesVms.Where(c => c.SessionTime <= DateTimeHelper.StartOfYear);

            var upcomingClassesVms = MapJourneyClassesInfosToViewModels(upcomingClassesInfos, authorsUserDict, purchaseVms, client);

            var upcomingThisWeekClasses = upcomingClassesVms
                .Where(c => c.SessionTime <= DateTimeHelper.StartOfNextWeek).ToList();

            var upcomingThisMonthClasses = upcomingClassesVms
                .Where(c => c.SessionTime <= DateTimeHelper.StartOfNextMonth)
                .Except(upcomingThisWeekClasses).ToList();

            var upcomingNextMonthClasses = upcomingClassesVms
                .Where(c => c.SessionTime <= DateTimeHelper.EndOfNextMonth)
                .Except(upcomingThisMonthClasses)
                .Except(upcomingThisWeekClasses).ToList();

            var upcomingThisYearClasses = upcomingClassesVms
                .Where(c => c.SessionTime <= DateTimeHelper.EndOfYear)
                .Except(upcomingNextMonthClasses)
                .Except(upcomingThisMonthClasses)
                .Except(upcomingThisWeekClasses).ToList();

            var upcomingAfterThisYearClasses = upcomingClassesVms
                .Where(c => c.SessionTime > DateTimeHelper.EndOfYear).ToList();

            var notBookedClassesVms = MapJourneyClassesInfosToViewModels(notBookedClassesInfos, authorsUserDict, purchaseVms, client);
            var otherUncompletedClassesVms = MapJourneyClassesInfosToViewModels(otherUncompletedClassesInfos, authorsUserDict, purchaseVms, client);

            var failedSubscription = await ListClientIncompleteSubscription(client.AccountId);

            var journeyClassesAllVm = new JourneyClassesAllViewModel
            {
                ClosestClassForBanner = !isClosestTimeIsInitial ? closestClassForBanner : null,
                Past = new JourneyPastClassesViewModel(),
                Upcoming = new JourneyUpcomingClassesViewModel(),
                ClientDeclinedSubscriptions = failedSubscription,
            };


            journeyClassesAllVm.Past.ThisWeek = pastThisWeekClasses;
            journeyClassesAllVm.Past.ThisMonth = pastThisMonthClasses;
            journeyClassesAllVm.Past.LastMonth = pastLastMonthClasses;
            journeyClassesAllVm.Past.ThisYear = pastThisYearClasses;
            journeyClassesAllVm.Past.PriorYears = pastPriorYearsClasses;
            journeyClassesAllVm.PastTotalCount = pastClassesVms.Count;

            journeyClassesAllVm.Upcoming.ThisWeek = upcomingThisWeekClasses;
            journeyClassesAllVm.Upcoming.ThisMonth = upcomingThisMonthClasses;
            journeyClassesAllVm.Upcoming.NextMonth = upcomingNextMonthClasses;
            journeyClassesAllVm.Upcoming.ThisYear = upcomingThisYearClasses;
            journeyClassesAllVm.Upcoming.AfterThisYear = upcomingAfterThisYearClasses;
            journeyClassesAllVm.Upcoming.OtherIncompleted = otherUncompletedClassesVms;
            journeyClassesAllVm.Upcoming.NotBooked = notBookedClassesVms;
            journeyClassesAllVm.UpcomingTotalCount = otherUncompletedClassesVms.Count + notBookedClassesVms.Count + upcomingClassesVms.Count;

            if (journeyClassesAllVm.UpcomingTotalCount + journeyClassesAllVm.PastTotalCount == 0)
            {
                return new JourneyClassesAllViewModel
                {
                    Upcoming = new JourneyUpcomingClassesViewModel
                    {
                        ThisWeek = ImmutableArray<JourneyClassViewModel>.Empty,
                        ThisMonth = ImmutableArray<JourneyClassViewModel>.Empty,
                        NextMonth = ImmutableArray<JourneyClassViewModel>.Empty,
                        ThisYear = ImmutableArray<JourneyClassViewModel>.Empty,
                        AfterThisYear = ImmutableArray<JourneyClassViewModel>.Empty,
                        OtherIncompleted = ImmutableArray<JourneyClassViewModel>.Empty,
                        NotBooked = ImmutableArray<JourneyClassViewModel>.Empty
                    },
                    ClientDeclinedSubscriptions = ImmutableArray<FailedSubscription>.Empty,
                    ClosestClassForBanner = new ClosestClassForBannerViewModel(),
                    Past = new JourneyPastClassesViewModel
                    {
                        ThisWeek = ImmutableArray<JourneyClassViewModel>.Empty,
                        ThisMonth = ImmutableArray<JourneyClassViewModel>.Empty,
                        LastMonth = ImmutableArray<JourneyClassViewModel>.Empty,
                        ThisYear = ImmutableArray<JourneyClassViewModel>.Empty,
                        PriorYears = ImmutableArray<JourneyClassViewModel>.Empty
                    },
                    PastTotalCount = 0,
                    UpcomingTotalCount = 0
                };
            }

            return journeyClassesAllVm;
        }

        private List<JourneyClassViewModel> MapJourneyClassesInfosToViewModels(
            List<JourneyClassInfo> classInfos,
            Dictionary<string, User> authorsUserDict,
            List<PurchaseViewModel> purchases,
            User client)
        {
            var journeyClassesVms = new List<JourneyClassViewModel>();

            foreach (var classInfo in classInfos)
            {
                if (authorsUserDict.ContainsKey(classInfo.AuthorUserId))
                {
                    var author = authorsUserDict[classInfo.AuthorUserId];
                    var purchase = purchases.First(p => p.ClientId == client.Id && p.ContributionId == classInfo.ContributionId);

                    var sessionTime = classInfo.SessionTimeUtc.HasValue
                        ? classInfo.SessionTimeUtc.Value
                        : (DateTime?)null;


                    journeyClassesVms.Add(new JourneyClassViewModel
                    {
                        Id = classInfo.ContributionId,
                        UserId = classInfo.AuthorUserId,
                        PreviewContentUrls = classInfo.PreviewContentUrls,
                        ServiceProviderName = $"{author.FirstName} {author.LastName}",
                        Type = classInfo.Type,
                        Title = classInfo.IsPrerecorded ? classInfo.SessionTitle : classInfo.ContributionTitle,
                        TotalNumberSessions = classInfo.TotalNumberSessions,
                        Rating = classInfo.Rating,
                        LikesNumber = classInfo.LikesNumber,
                        PurchaseDateTime = DateTimeHelper.GetZonedDateTimeFromUtc(purchase.Payments.Min(p => p.DateTimeCharged), client.TimeZoneId),
                        SessionTime = sessionTime,
                        NumberCompletedSessions = classInfo.NumberCompletedSessions,
                        SessionId = classInfo.SessionId,
                        IsPrerecorded = classInfo.IsPrerecorded,
                        PercentageCompleted = classInfo.PercentageCompleted,
                        IsCompleted = classInfo.IsCompleted,
                        SessionTimes = classInfo.SessionTimes,
                        TimezoneId = classInfo.TimezoneId,
                        IsWorkshop = classInfo.IsWorkshop,
                        TimeZoneShortForm = classInfo.TimeZoneShortForm,
                    });
                }
            }

            return journeyClassesVms;
        }

        public async Task<OperationResult> GetForAllClientPurchasesAsync(string requestorAccountId, string userId)
        {
            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == requestorAccountId);
            if (requestorAccount == null)
            {
                return OperationResult.Failure($"Account with Id '{requestorAccountId}' not found. Unable to get purchased contributions");
            }

            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);

            var isGetAllowed = requestorAccountId == client.AccountId ||
                               requestorAccount.Roles.Contains(Roles.Admin) ||
                               requestorAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isGetAllowed)
            {
                return OperationResult.Failure("Forbidden to get not owned purchases");
            }

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == userId || p.ClientId.ToLower() == $"delete-{userId}");
            var journeyPagePurchases = new List<JourneyPagePurchaseViewModel>();

            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            //purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
            var purchaseSucceededVmsList = purchaseVms.ToList();

            if (purchaseSucceededVmsList.Any())
            {
                var contributionIds = purchaseSucceededVmsList.Select(c => c.ContributionId);
                var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
                var purchasedContributions = await _contributionRootService.Get(c => contributionIds.Contains(c.Id));
                var purchasedContributionsList = purchasedContributions.ToList();

                var boughtContributionsAuthorIds = purchasedContributionsList.Select(c => c.UserId);
                var authors = await _unitOfWork.GetRepositoryAsync<User>().Get(u => boughtContributionsAuthorIds.Contains(u.Id));
                var authorsList = authors.ToList();

                foreach (var purchaseSucceeded in purchaseSucceededVmsList)
                {
                    try
                    {
                        var purchasedContribution = purchasedContributionsList.FirstOrDefault(c => c.Id == purchaseSucceeded.ContributionId);
                        if (purchasedContribution == default)
                        {
                            continue;
                        }

                        var authorUser = authorsList.First(a => a.Id == purchasedContribution.UserId);
                        var model = new JourneyPagePurchaseViewModel
                        {
                            ContributionId = purchaseSucceeded.ContributionId,
                            CreateTime = purchaseSucceeded.CreateTime,
                            IsAccessRevokedByCoach = purchaseSucceeded.ClientId.ToLower().StartsWith("delete"),
                            AuthorUserId = authorUser.Id,
                            AuthorAvatarUrl = authorUser.AvatarUrl,
                            PreviewContentUrls = purchasedContribution.PreviewContentUrls,
                            ServiceProviderName = $"{authorUser.FirstName} {authorUser.LastName}",
                            Type = purchasedContribution.Type,
                            Title = purchasedContribution.Title,
                            Rating = purchasedContribution.Rating,
                            LikesNumber = purchasedContribution.LikesNumber,
                            IsWorkshop = purchasedContribution.IsWorkshop,
                            IsMembersHiddenInCommunity = purchasedContribution.IsMembersHiddenInCommunity,
                            PurchaseDateTime = DateTimeHelper.GetZonedDateTimeFromUtc(purchaseSucceeded.Payments.Min(p => p.DateTimeCharged), client.TimeZoneId),
                            GroupCourseSessions = purchasedContribution is SessionBasedContribution sessionBased ? sessionBased.Sessions : null,
                            OneToOneSession = purchasedContribution is ContributionOneToOne oneToOneBased ? oneToOneBased.OneToOneSessionDataUi : null,
                            PercentageCompleted = purchasedContribution is SessionBasedContribution sessionBasedForPercentage ? GetCompletedPercentageOfContribution(_mapper.Map<SessionBasedContributionViewModel>(sessionBasedForPercentage), false, userId) : 0
                        };
                        //Extra Parameters
                        model.Participants = await GetParticipantsVmsAsync(purchasedContribution.Id);
                        if (client.LastReadSocialInfos.ContainsKey(purchasedContribution.Id))
                        {
                            var postsCount = await postsRepository.Count(x =>
                                x.ContributionId == purchasedContribution.Id && !x.IsDraft && x.CreateTime > client.LastReadSocialInfos[purchasedContribution.Id]);
                            model.UnReadPostCount = postsCount;
                        }
                        if (purchasedContribution is SessionBasedContribution sessionBasedModel)
                        {
                            var upcomingLiveSessionCount = sessionBasedModel.Sessions?.SelectMany(x => x.SessionTimes?.Where(m => !m.IsCompleted && !x.IsPrerecorded)).Count();
                            var upcomingselfpacedSessionCount = sessionBasedModel.Sessions?.SelectMany(x => x.SessionTimes?.Where(m => !m.CompletedSelfPacedParticipantIds.Contains(userId) && x.IsPrerecorded)).Count();
                            if (upcomingLiveSessionCount == 0 && upcomingselfpacedSessionCount == 0)
                            {
                                model.IsPast = true;
                                journeyPagePurchases.Add(model);
                            }
                            else
                            {
                                model.IsUpcoming = true;
                                journeyPagePurchases.Add(model);
                            }
                        }
                        else if (purchasedContribution is ContributionOneToOne oneToOneBasedSession)
                        {
                            var upcomingSessionCount = oneToOneBasedSession.AvailabilityTimes?.SelectMany(x => x.BookedTimes?.Where(m => !m.IsCompleted)).Count();
                            if (upcomingSessionCount == 0)
                            {
                                model.IsPast = true;
                                journeyPagePurchases.Add(model);
                            }
                            else
                            {
                                model.IsUpcoming = true;
                                journeyPagePurchases.Add(model);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"GetForAllClientPurchasesAsync at {DateTime.UtcNow}.", e.Message);
                    }
                }
            }

            return OperationResult.Success(string.Empty, journeyPagePurchases?.OrderByDescending(m => m.CreateTime).ToList());
        }

        //private List<JourneyContributionBriefViewModel> GetClassesBriefVmsFromContribution(
        //    List<ContributionBaseViewModel> contributionsVms,
        //    List<User> authors,
        //    List<PurchaseViewModel> purchases,
        //    User client,
        //    bool arePastContributions)
        //{
        //    var journeyContributionBriefVms = new List<JourneyContributionBriefViewModel>();

        //    if (contributionsVms.Count > 0)
        //    {
        //        foreach (var contributionVm in contributionsVms)
        //        {
        //            var author = authors.First(a => a.Id == contributionVm.UserId);
        //            var purchase = purchases.First(p => p.ClientId == client.Id && p.ContributionId == contributionVm.Id);

        //            int? numCompletedSessions = contributionVm.GetNumberOfCompletedClassesForParticipant(client.Id);
        //            int? totalNumberSessions = contributionVm.GetTotalNumClassesForParticipant(client.Id);

        //            var sessionTimesUtc = arePastContributions
        //                ? contributionVm.GetClassTimesUtcForParticipant(client.Id)
        //                : contributionVm.GetUpcomingClassTimesUtcForParticipant(client.Id);

        //            foreach (var sessionTimeUtc in sessionTimesUtc)
        //            {
        //                journeyContributionBriefVms.Add(new JourneyContributionBriefViewModel
        //                {
        //                    Id = contributionVm.Id,
        //                    UserId = contributionVm.UserId,
        //                    PreviewContentUrls = contributionVm.PreviewContentUrls,
        //                    ServiceProviderName = $"{author.FirstName} {author.LastName}",
        //                    Type = contributionVm.Type,
        //                    Title = contributionVm.Title,
        //                    TotalNumberSessions = totalNumberSessions,
        //                    Rating = contributionVm.Rating,
        //                    LikesNumber = contributionVm.LikesNumber,
        //                    PurchaseDateTime = DateTimeHelper.GetZonedDateTimeFromUtc(purchase.Payments.Min(p => p.DateTimeCharged), client.TimeZoneId),
        //                    SessionTime = DateTimeHelper.GetZonedDateTimeFromUtc(sessionTimeUtc, client.TimeZoneId),
        //                    NumberCompletedSessions = numCompletedSessions
        //                });
        //            }

        //            if (!sessionTimesUtc.Any())
        //            {
        //                journeyContributionBriefVms.Add(new JourneyContributionBriefViewModel
        //                {
        //                    Id = contributionVm.Id,
        //                    UserId = contributionVm.UserId,
        //                    PreviewContentUrls = contributionVm.PreviewContentUrls,
        //                    ServiceProviderName = $"{author.FirstName} {author.LastName}",
        //                    Type = contributionVm.Type,
        //                    Title = contributionVm.Title,
        //                    TotalNumberSessions = totalNumberSessions,
        //                    Rating = contributionVm.Rating,
        //                    LikesNumber = contributionVm.LikesNumber,
        //                    PurchaseDateTime = DateTimeHelper.GetZonedDateTimeFromUtc(purchase.Payments.Min(p => p.DateTimeCharged), client.TimeZoneId),
        //                    SessionTime = null,
        //                    NumberCompletedSessions = numCompletedSessions
        //                });
        //            }
        //        }
        //    }

        //    return journeyContributionBriefVms;
        //}

        public async Task<OperationResult> Delete(string id)
        {
            var existedContribution = await _contributionRootService.GetOne(id);

            if (existedContribution != null)
            {
                var existedContributionVm = _mapper.Map<ContributionBaseViewModel>(existedContribution);

                //if (existedContributionVm.GetBookedParticipantsIds().Count > 0)
                //{
                //    return OperationResult.Failure("Contribution cannot be deleted. It has participants applied for class(es)");
                //}

                var imageDeletionResults = new List<OperationResult>();

                // TODO Replace with brand new created DeletePublicImages method once more than one image should be deleted
                if (existedContribution.PreviewContentUrls.Count > 0 &&
                    existedContribution.PreviewContentUrls.Any(p => p != null))
                {
                    foreach (var imageUrl in existedContribution.PreviewContentUrls)
                    {
                        var imageDeletionResult = await _fileManager.DeleteFileFromPublicStorageByUrlAsync(imageUrl);

                        imageDeletionResults.Add(imageDeletionResult);
                    }

                    if (imageDeletionResults.Any(r => r.Succeeded == false))
                    {
                        return OperationResult.Failure(
                            "Unable to delete contribution. Some related images are not deleted");
                    }
                }

                var attachmentsKeys = existedContributionVm.AttachmentsKeys;
                if (attachmentsKeys.Any())
                {
                    await _fileManager.DeleteFilesFromNonPublicStorageAsync(attachmentsKeys);
                }

                long numDeleted;
                try
                {
                    numDeleted = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Delete(id);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Unable to delete contribution with Id {id} from DB. Exception: {ex.Message}. Chat {existedContribution.Chat.Sid} is not deleted yet");

                    return OperationResult.Failure("Error occured during contribution deletion, try again later and if the problem persists contact support");
                }

                if (numDeleted > 0)
                {
                    if (existedContribution is SessionBasedContribution && existedContribution.Chat != null)
                    {
                        var chatDeletionResult =
                            await _chatService.DeleteChatForContribution(existedContribution.Chat.Sid);
                        if (!chatDeletionResult.Succeeded)
                        {
                            _logger.Log(LogLevel.Error,
                                $"Unable to delete chat associated with contribution Id {id} on vendor end due to error {chatDeletionResult.Message}. Although the contribution has already been deleted. To avoid orphaned chat channels please delete chat with id {existedContribution.Chat.Sid} manually");
                            return OperationResult.Success(
                                $"Contribution has been deleted. But chat associated with it has not. Please contact support with request to manually delete chat room and provide the following data: Chat room Id: {existedContribution.Chat.Sid}; Time occured: {DateTime.UtcNow}");
                        }
                    }

                    if (existedContribution is ContributionOneToOne && existedContribution.Chat != null &&
                        existedContribution.Chat.CohealerPeerChatSids.Any())
                    {
                        List<OperationResult> chatDeletionFaulureResults = new List<OperationResult>();
                        foreach (var userIdChatSidPair in existedContribution.Chat.CohealerPeerChatSids)
                        {
                            var chatDeletionResult = await _chatService.DeleteChatForContribution(userIdChatSidPair.Value);
                            if (!chatDeletionResult.Succeeded)
                            {
                                _logger.Log(LogLevel.Error,
                                    $"Unable to delete chat associated with contribution Id {id} on vendor end due to error {chatDeletionResult.Message}. Although the contribution has already been deleted. To avoid orphaned chat channels please delete chat with id {existedContribution.Chat.Sid} manually");
                                chatDeletionFaulureResults.Add(OperationResult.Failure(
                                    $"Contribution has been deleted. But chat associated with it has not. Please contact support with request to manually delete chat room and provide the following data: Chat room Id: {existedContribution.Chat.Sid}; Time occured: {DateTime.UtcNow}"));
                            }
                        }

                        if (chatDeletionFaulureResults.Any())
                        {
                            var resultsMessage = chatDeletionFaulureResults.Aggregate(string.Empty, (current, item) => current + item + " \r\n");
                            return OperationResult.Failure(resultsMessage);
                        }
                    }

                    return OperationResult.Success("Number of contributions has been deleted", numDeleted);
                }

                return OperationResult.Failure(
                    $"Not deleted contribution with id {id}. Try again later and if the problem persist contact support");
            }

            return OperationResult.Failure($"Contribution with the following Id not found: {id}");
        }

        private int GetCompletedPercentageOfContribution(SessionBasedContributionViewModel contribution, bool Iscohealer, string requesterUserId = null)
        {
            int totalSessionsCount = 0;
            double completedLiveSessionPercentage = 0.0;
            double completedSelfPacedSessionsPercentage = 0.0;
            int percentageCompleted = 0;

            totalSessionsCount = contribution.Sessions.Count();

            if (Iscohealer)
            {
                totalSessionsCount = contribution.Sessions.Count(s => !s.IsPrerecorded);
                foreach (var session in contribution.Sessions)
                {
                    if (!session.IsPrerecorded)
                    {
                        if (session.SessionTimes?.Count() > 0)
                        {
                            double weightingFactor = 1 / (double)session.SessionTimes.Count();
                            completedLiveSessionPercentage += session.SessionTimes.Count(st => st.IsCompleted) * weightingFactor;
                        }
                    }
                }
                percentageCompleted = totalSessionsCount > 0 ? Convert.ToInt32(Math.Ceiling(100 * completedLiveSessionPercentage / totalSessionsCount)) : 0;
                return percentageCompleted;
            }
            else
            {
                totalSessionsCount = contribution.Sessions.Count();
                foreach (var session in contribution.Sessions)
                {
                    if (!session.IsPrerecorded)
                    {
                        if (session.SessionTimes?.Count() > 0)
                        {
                            double weightingFactor = 1 / (double)session.SessionTimes.Count();
                            completedLiveSessionPercentage += session.SessionTimes.Count(st => st.IsCompleted) * weightingFactor;
                        }
                    }

                    if (session.IsPrerecorded)
                    {
                        if (session.SessionTimes?.Count() > 0 && session.SessionTimes?.Count(s => s.CompletedSelfPacedParticipantIds.Contains(requesterUserId)) > 0)
                        {
                            double weightingFactor = 1 / (double)session.SessionTimes.Count();
                            completedSelfPacedSessionsPercentage += (session.SessionTimes.Count(s => s.CompletedSelfPacedParticipantIds.Contains(requesterUserId)) * weightingFactor);
                        }
                    }
                }
                percentageCompleted = totalSessionsCount > 0 ? Convert.ToInt32(Math.Ceiling(100 * (completedLiveSessionPercentage + completedSelfPacedSessionsPercentage) / totalSessionsCount)) : 0;
                return percentageCompleted;
            }
        }
        public async Task<ContributionBaseViewModel> GetClientContributionByIdAsync(string id, string accountId = null)
        {
            var contribution = await _contributionRootService.GetOne(c => c.Id == id && (c.Status == ContributionStatuses.Approved || c.Status == ContributionStatuses.Completed));

            if (contribution is null)
            {
                return null;
            }

            var author = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            
            var profilepage = await _profilePageService.GetProfilePage(author.AccountId);
            var coachSelectedDarkMode = false;
            if (profilepage != null)
            {
                if(profilepage.IsDarkModeEnabled)
                    coachSelectedDarkMode = profilepage.IsDarkModeEnabled;
            }

            var coachCountry = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(c => c.Id == author.CountryId);

            var contributionClientVm = _mapper.Map<ContributionBaseViewModel>(contribution);

            var contributionTestimonials = await _unitOfWork.GetRepositoryAsync<Testimonial>().Get(m => m.ContributionId == contribution.Id);
            if (contributionTestimonials.Count() > 0)
            {
                contributionClientVm.testimonials = contributionTestimonials.ToList();
            }

            await FillPodsForSessionContribution(contributionClientVm);

            var paymentStatus = Constants.Contribution.Payment.Statuses.Unpurchased;
            User requesterUser = null;
            PurchaseViewModel purchaseVm = null;
            int totalSessionsCount = 0;
            if (accountId is null)
            {
                contributionClientVm.TimeZoneId = author.TimeZoneId;
                var coachTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == author.TimeZoneId);
                contributionClientVm.TimeZoneShortForm = coachTimeZone?.ShortName;
            }
            else
            {

                requesterUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
                contribution = await ValidateChatAndParticipants(contribution, requesterUser);

                if (!string.IsNullOrEmpty(author?.CountryId))
                {
                    var availableCurrencies = await GetCurrenciesForContribution(author.CountryId);
                    contributionClientVm.AvailableCurrencies = availableCurrencies.ToList();
                }


                contributionClientVm.TimeZoneId = requesterUser.TimeZoneId;
                var clientTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == requesterUser.TimeZoneId);
                contributionClientVm.TimeZoneShortForm = clientTimeZone?.ShortName;

                contributionClientVm.ClosestClassForBanner =
                    contributionClientVm.GetClosestClientClassForBanner(requesterUser.Id, author.TimeZoneId);

                if (contributionClientVm is ContributionOneToOneViewModel &&
                    contributionClientVm.ClosestClassForBanner != null)
                {
                    contributionClientVm.ClosestClassForBanner.Title = $"{author.FirstName} {author.LastName}";
                }

                var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                     .GetOne(p => p.ClientId == requesterUser.Id && p.ContributionId == id);

                if (purchase == null)
                {
                    //try get deleted purchase
                    purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                    .GetOne(p => p.ClientId.ToLower() == $"delete-{requesterUser.Id}" && p.ContributionId == id);
                }

                purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
                paymentStatus = purchaseVm?.ActualPaymentStatus ?? paymentStatus;

                if (purchaseVm != null && purchaseVm.HasAccessToContribution && !purchase.Payments.LastOrDefault().IsAccessRevoked)
                {
                    contributionClientVm.Participants = await GetParticipantsVmsAsync(id, requesterUser);
                    contributionClientVm.Notes =
                        await _noteService.GetContributionNotesAsync(accountId, contributionClientVm.Id);
                    contributionClientVm.IsPurchased = true;
                    contributionClientVm.IsAccessRevokedByCoach = purchase.Payments.LastOrDefault().IsAccessRevokedByCoach;

                    contributionClientVm.AssignChatSidForUserContributionPage(requesterUser.Id);

                    AttachSubscriptionStatus(
                        contributionClientVm,
                        contribution.PaymentInfo.MembershipInfo?.PaymentOptionsMap,
                        purchaseVm);

                    AddMissingParticipants(contribution.Id, requesterUser.Id).GetAwaiter().GetResult();
                }
                if (contributionClientVm is SessionBasedContributionViewModel vm)
                {
                    totalSessionsCount = vm.Sessions.Count();
                    foreach (var session in vm.Sessions)
                    {
                        foreach (var sessionTime in session.SessionTimes)
                        {
                            var participantsIds = sessionTime.ParticipantsIds.Where(a => a == requesterUser.Id).ToList();
                            if (!string.IsNullOrEmpty(sessionTime.PodId) && vm.Pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds != null)
                            {
                                participantsIds.AddRange(vm.Pods.FirstOrDefault(x => x.Id == sessionTime.PodId).ClientIds);
                            }

                            sessionTime.ParticipantInfos =
                                await GetSessionsTimeParticipantsInfoAsync(participantsIds);

                            session.IsHappeningToday = IsSessionToday(sessionTime.StartTime);
                        }
                    }
                    contributionClientVm.PercentageCompleted = GetCompletedPercentageOfContribution(vm, false, requesterUser.Id);
                }
                else if(contributionClientVm is ContributionOneToOneViewModel oneToOneBased)
                {
                    var bookTimes = oneToOneBased.AvailabilityTimes?.SelectMany(m => m.BookedTimes).ToList();
                    var participantIds = new List<string>();
                    foreach(var bt in bookTimes)
                    {
                        participantIds.Add(bt.ParticipantId);
                    }

                    if (participantIds.Count > 0)
                    {
                        oneToOneBased.ParticipantInfos =
                                await GetSessionsTimeParticipantsInfoAsync(participantIds);
                    }
                    
                }

                List<string> partnerCoachIds = contributionClientVm.Partners.Select(p => p.UserId).ToList();
                foreach (var partnerCoachId in partnerCoachIds)
                {
                    if (!string.IsNullOrEmpty(partnerCoachId))
                    {
                        var partnerCoach = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == partnerCoachId);
                        var peerChatResult = await _chatService.CreatePeerChat(partnerCoach.AccountId, requesterUser.Id);
                        var participantClientPeerChat = peerChatResult.Payload as PeerChat;
                        if (participantClientPeerChat != null)
                        {
                            contribution.Chat.PartnerClientChatSid.TryAdd(partnerCoachId, participantClientPeerChat.Sid);
                        }
                    }
                }
            }
            contributionClientVm.ClearHiddenForClientInfo(requesterUser?.Id, purchaseVm);
            contributionClientVm.ConvertAllOwnUtcTimesToZoned(contributionClientVm.TimeZoneId);
            contributionClientVm.ServiceProviderName = $"{author.FirstName} {author.LastName}";
            contributionClientVm.PurchaseStatus = paymentStatus;
            contributionClientVm.Bio = author.Bio;
            contributionClientVm.CoachCountry = coachCountry?.Name;

            Dictionary<string, string> LegacyColors = new Dictionary<string, string>();
            LegacyColors.Add("PrimaryColorCode", "#CDBA8F");
            LegacyColors.Add("AccentColorCode", "#116582");
            LegacyColors.Add("TertiaryColorCode", "#F6E8BO");

            if (contributionClientVm.IsCustomBrandingColorsActive)
            {
                if (contributionClientVm?.BrandingColors == null || contributionClientVm?.BrandingColors.Count == 0 || (contributionClientVm.BrandingColors.Count == LegacyColors.Count && !contributionClientVm.BrandingColors.Except(LegacyColors).Any()))
                {
                    contributionClientVm.CoachSelectedBrandingColors = author?.BrandingColors;
                    contributionClientVm.CustomLogo = author?.CustomLogo;
                }
                else
                {
                    contributionClientVm.CoachSelectedBrandingColors = contributionClientVm.BrandingColors;
                    contributionClientVm.CustomLogo = contributionClientVm.CustomLogo;
                }
            }
            else
            {
                contributionClientVm.CoachSelectedBrandingColors = LegacyColors;

                contributionClientVm.CustomLogo = null;
            }
            //contributionClientVm.CoachSelectedBrandingColors = author?.BrandingColors;
            contributionClientVm.IsCoachSelectedDarkModeEnabled = coachSelectedDarkMode;
            contributionClientVm.CoachAvatarUrl = author?.AvatarUrl;
            var contributionPartners = await GetContributionPartnersAsync(contribution.Id);
            if (contributionPartners.Succeeded)
            {
                contributionClientVm.ContributionPartners = contributionPartners.Payload;
            }

            if (contributionClientVm.PurchaseStatus == Constants.Contribution.Payment.Statuses.ProceedSubscription)
            {
                contributionClientVm.PaymentInfo.PaymentOptions.Remove(PaymentOptions.EntireCourse.ToString());
            }
            if(contribution is ContributionOneToOne)
                contributionClientVm.StripeAccount = ((ContributionOneToOne)contribution).CoachStandardAccountId;

            return contributionClientVm;
        }
        private async Task AddMissingParticipants(string contributionId, string userId)
        {
            try
            {
                var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(m => m.Id == contributionId);
                var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(m => m.ClientId == userId && m.ContributionId == contributionId);
                if (purchase is not null && contribution is not null)
                {
                    UpdateContributionDataWithMissingParticipants(contribution, userId, purchase).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{ex.Message} in AddMissingParticipants");
            }

        }
        private async Task UpdateContributionDataWithMissingParticipants(ContributionBase contribution, string userId, Purchase purchase)
        {
            if (contribution is SessionBasedContribution sessionbased)
            {
                foreach (var session in sessionbased.Sessions.Where(session => !session.IsPrerecorded && session.SessionTimes.Count == 1))
                {
                    foreach (var sessionTime in session.SessionTimes.Where(m => !m.IsCompleted))
                    {
                        var participantsIds = sessionTime.ParticipantsIds;
                        if (!participantsIds.Contains(userId) && participantsIds.Count < session.MaxParticipantsNumber && sessionTimeExistInPurchaseBookedList(purchase, sessionTime?.Id))
                        {
                            participantsIds.Add(userId);
                        }
                    }
                }
            }
            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
        }
        private bool sessionTimeExistInPurchaseBookedList(Purchase purchase, string sessionTimeId)
        {
            if (purchase == null || sessionTimeId is null)
            {
                return false;
            }

            foreach (var payment in purchase.Payments)
            {
                if (payment?.PaymentStatus == Entity.Enums.Payments.PaymentStatus.Succeeded && payment.BookedClassesIds?.Contains(sessionTimeId) == true)
                    return true;
            }
            return false;
        }
        private async Task<ContributionBase> ValidateChatAndParticipants(ContributionBase contribution, User user)
        {
            GroupChat groupChat = contribution.Chat;
            if (groupChat == null)
            {
                var existingChatRsesult = await _chatService.GetExistingChatSidByUniueName(contribution);
                if (existingChatRsesult.Succeeded && !string.IsNullOrEmpty(existingChatRsesult?.Payload))
                {
                    groupChat = new GroupChat
                    {
                        Sid = existingChatRsesult.Payload,
                        FriendlyName = contribution.Title,
                        PreviewImageUrl = contribution.PreviewContentUrls.FirstOrDefault()
                    };
                }
                else
                {
                    if (!contribution.IsGroupChatHidden)
                    {
                        var result = await _chatService.CreateChatForContribution(contribution);
                        if (result.Succeeded)
                        {
                            groupChat = result.Payload;
                        }
                    }
                    
                }
                if (!string.IsNullOrEmpty(groupChat?.Sid))
                {
                    contribution.Chat = groupChat;
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                }
            }

            if (!string.IsNullOrEmpty(groupChat?.Sid))
            {
                // check if we need to add users to the chat
                if (contribution.Id != "63530ba59f737d0efde13e76")
                {
                    var participants = await GetParticipantsVmsAsync(contribution.Id, user);
                    participants = participants?.Where(p => p.Id != contribution.UserId)?.ToList();
                    foreach (var participant in participants)
                    {
                        if (!groupChat.CohealerPeerChatSids.ContainsKey(participant.Id))
                        {
                            var res = await _chatService.AddClientToContributionRelatedChat(participant.Id, contribution);
                            if (res.Failed)
                                break;
                        }
                    }
                }
                // check if we need to assign partner check logic
                bool needToUpdateContr = false;
                foreach (var partner in contribution.Partners)
                {
                    var partnerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == partner.UserId);
                    if (partnerUser != null)
                    {
                        if (groupChat.PartnerChats.FirstOrDefault(p => p.PartnerUserId == partnerUser.Id) == null)
                        {
                            var partnerAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == partnerUser.AccountId);
                            if (partnerAccount != null)
                            {
                                var existingChatsUserIds = groupChat.CohealerPeerChatSids.Select(x => x.Key);
                                List<PartnerPeerChat> partnerChats = new List<PartnerPeerChat>();
                                foreach (var clientUserId in existingChatsUserIds)
                                {
                                    var peerChatResult = await _chatService.CreatePeerChat(partnerAccount.Id, clientUserId);
                                    if (peerChatResult.Succeeded)
                                    {
                                        var peerChat = peerChatResult.Payload as PeerChat;
                                        partnerChats.Add(new PartnerPeerChat
                                        {
                                            UserId = clientUserId,
                                            ChatSid = peerChat.Sid
                                        });
                                    }
                                }
                                needToUpdateContr = true;
                                groupChat.PartnerChats.Add(new PartnerChats
                                {
                                    PartnerUserId = partnerUser.Id,
                                    PeerChats = partnerChats
                                });
                            }
                        }
                    }
                }

                if (needToUpdateContr)
                {
                    contribution.Chat = groupChat;
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                }
            }

            return await _contributionRootService.GetOne(c => c.Id == contribution.Id && (c.Status == ContributionStatuses.Approved || c.Status == ContributionStatuses.Completed));
        }

        private void AttachSubscriptionStatus(
            ContributionBaseViewModel contributionClientVm,
            Dictionary<string, PaymentOptions> paymentOptionMap,
            PurchaseViewModel purchaseVm)
        {
            if (!(contributionClientVm is ContributionMembershipViewModel) && !(contributionClientVm is ContributionCommunityViewModel))
            {
                return;
            }

            var contributionMembershipViewModel = contributionClientVm as ContributionMembershipViewModel;
            var contributionCommunityViewModel = contributionClientVm as ContributionCommunityViewModel;

            var subscription = purchaseVm.Subscription;

            if (subscription.Status != "active" && subscription.Status != "trialing")
            {
                return;
            }

            if (subscription.CancelAtPeriodEnd)
            {
                if (contributionClientVm is ContributionMembershipViewModel)
                {
                    contributionMembershipViewModel.SubscriptionStatus = new SubscriptionStatus()
                    {
                        Status = "cancel",
                        EndPeriod = subscription.CurrentPeriodEnd,
                        PaymentOption = GetPaymentOptions(paymentOptionMap, subscription).ToString(),
                    };
                }
                else
                {
                    contributionCommunityViewModel.SubscriptionStatus = new SubscriptionStatus()
                    {
                        Status = "cancel",
                        EndPeriod = subscription.CurrentPeriodEnd,
                        PaymentOption = GetPaymentOptions(paymentOptionMap, subscription).ToString(),
                    };
                }

                return;
            }

            if (!string.IsNullOrEmpty(subscription.ScheduleId))
            {
                var (currentPaymentOption, nextPaymentOption) = GetPhasesPaymentOptions(subscription, paymentOptionMap);

                if (contributionClientVm is ContributionMembershipViewModel)
                {
                    contributionMembershipViewModel.SubscriptionStatus = new SubscriptionStatus()
                    {
                        Status = "update",
                        EndPeriod = subscription.CurrentPeriodEnd,
                        PaymentOption = currentPaymentOption.ToString(),
                        NextPaymentOption = nextPaymentOption.ToString(),
                    };
                }
                else
                {
                    contributionCommunityViewModel.SubscriptionStatus = new SubscriptionStatus()
                    {
                        Status = "update",
                        EndPeriod = subscription.CurrentPeriodEnd,
                        PaymentOption = currentPaymentOption.ToString(),
                        NextPaymentOption = nextPaymentOption.ToString(),
                    };
                }
                return;
            }

            if (contributionClientVm is ContributionMembershipViewModel)
            {
                contributionMembershipViewModel.SubscriptionStatus = new SubscriptionStatus()
                {
                    Status = "active",
                    EndPeriod = subscription.CurrentPeriodEnd,
                    PaymentOption = GetPaymentOptions(paymentOptionMap, subscription).ToString(),
                };
            }
            else
            {
                contributionCommunityViewModel.SubscriptionStatus = new SubscriptionStatus()
                {
                    Status = "active",
                    EndPeriod = subscription.CurrentPeriodEnd,
                    PaymentOption = GetPaymentOptions(paymentOptionMap, subscription).ToString(),
                };
            }
        }

        private PaymentOptions GetPaymentOptions(Dictionary<string, PaymentOptions> paymentOptionMap,
            string planId)
        {
            if (paymentOptionMap.TryGetValue(planId, out var targetPaymentOption))
            {
                return targetPaymentOption;
            }

            _logger.LogError($"error during getting payment option by product plan id {planId}");
            throw new Exception("plan not found");
        }

        private PaymentOptions GetPaymentOptions(Dictionary<string, PaymentOptions> paymentOptionMap,
            Subscription subscription)
        {
            if (subscription?.Id == "-2")
            {
                return PaymentOptions.MonthlyMembership;
            }
            if (subscription.Status == "trialing")
            {
                return PaymentOptions.Trial;
            }

            return GetPaymentOptions(paymentOptionMap, subscription.Plan.Id);
        }

        private (PaymentOptions current, PaymentOptions next) GetPhasesPaymentOptions(Subscription subscription, Dictionary<string, PaymentOptions> paymentOptionMap)
        {
            var nextPhasePlanId = subscription
                .Schedule
                .Phases.LastOrDefault()
                ?.Plans.FirstOrDefault()
                ?.PlanId;

            if (!string.IsNullOrEmpty(nextPhasePlanId))
            {
                return (
                    GetPaymentOptions(paymentOptionMap, subscription),
                    GetPaymentOptions(paymentOptionMap, nextPhasePlanId));
            }

            _logger.LogError("next phase plan Id is null");
            throw new Exception("next phase plan Id is null");
        }

        public async Task<OperationResult> GetCohealerContributionByIdAsync(string id, string accountId)
        {
            var contribution = await _contributionRootService.GetOne(id);

            if (contribution != null)
            {
                var userRepository = _unitOfWork.GetRepositoryAsync<User>();
                var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
                var author = await userRepository.GetOne(e => e.Id == contribution.UserId);
                var profilepage = await _profilePageService.GetProfilePage(author.AccountId);
                var coachSelectedDarkMode = false;
                if (profilepage != null)
                {
                    if (profilepage.IsDarkModeEnabled)
                        coachSelectedDarkMode = profilepage.IsDarkModeEnabled;
                }
                var requesterAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == accountId);
                var requesterUser = await userRepository.GetOne(u => u.AccountId == accountId);
                var isRequesterIsAdmin = requesterAccount.Roles.Contains(Roles.Admin) || requesterAccount.Roles.Contains(Roles.SuperAdmin);
                var isRequesterPartner = contribution.Partners.Any(x => x.IsAssigned && x.UserId == requesterUser.Id);
                if (contribution.UserId == requesterUser.Id
                    || isRequesterPartner
                    || isRequesterIsAdmin)
                {
                    var contributionCohealerVm = _mapper.Map<ContributionBaseViewModel>(contribution);
                    
                    var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(x => x.ContributionId == contributionCohealerVm.Id);
                    contributionCohealerVm.DeletingAllowed = purchase == null ? true : false;
                    
                    if (!isRequesterIsAdmin)
                    {

                        contributionCohealerVm.ClosestClassForBanner = contributionCohealerVm.GetClosestCohealerClassForBanner(author.TimeZoneId);

                        if (contributionCohealerVm is ContributionOneToOneViewModel && contributionCohealerVm.ClosestClassForBanner != null)
                        {
                            var participant = await _unitOfWork.GetRepositoryAsync<User>()
                                .GetOne(u => u.Id == contributionCohealerVm.ClosestClassForBanner.OneToOneParticipantId);

                            contributionCohealerVm.ClosestClassForBanner.Title = $"{participant.FirstName} {participant.LastName}";
                        }

                        if (contributionCohealerVm is SessionBasedContributionViewModel vm)
                        {
                            var podIds = vm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                            var pods = await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id));
                            foreach (var session in vm.Sessions)
                            {
                                foreach (var sessionTime in session.SessionTimes)
                                {
                                    var participantsIds = sessionTime.ParticipantsIds;

                                    if (!string.IsNullOrEmpty(sessionTime.PodId) && pods.FirstOrDefault(x => x.Id == sessionTime.PodId)?.ClientIds != null)
                                    {
                                        participantsIds.AddRange(pods.FirstOrDefault(x => x.Id == sessionTime.PodId).ClientIds);
                                    }
                                    
                                    sessionTime.ParticipantInfos =
                                        await GetSessionsTimeParticipantsInfoAsync(participantsIds);
                                    
                                    session.IsHappeningToday = IsSessionToday(sessionTime.StartTime);
                                }
                                
                            }
                            contributionCohealerVm.PercentageCompleted = GetCompletedPercentageOfContribution(vm, true);
                        }

                    }
                    contributionCohealerVm.ServiceProviderName = $"{requesterUser.FirstName} {requesterUser.LastName}";

                    if (contribution.Status == ContributionStatuses.Approved)
                    {
                        contributionCohealerVm.Participants = await GetParticipantsVmsAsync(id);
                    }

                    if (isRequesterPartner)
                    {
                        var contributionOwner = await userRepository.GetOne(x => x.Id == contribution.UserId);
                        contributionCohealerVm.ServiceProviderName = $"{contributionOwner.FirstName} {contributionOwner.LastName}";

                        var partnerChats = contributionCohealerVm.Chat.PartnerChats.FirstOrDefault(x => x.PartnerUserId == requesterUser.Id);
                        var chats = partnerChats.PeerChats.ToDictionary(key => key.UserId, value => value.ChatSid);
                        contributionCohealerVm.Chat.CohealerPeerChatSids = chats;
                        contributionCohealerVm.Chat.PartnerChats = null;
                    }

                    contributionCohealerVm.ConvertAllOwnUtcTimesToZoned(requesterUser.TimeZoneId);
                    contributionCohealerVm.EarnedRevenue = await _cohealerIncomeService.GetContributionRevenueAsync(contributionCohealerVm.Id);
                    contributionCohealerVm.Notes = await _noteService.GetContributionNotesAsync(accountId, contributionCohealerVm.Id);

                    Dictionary<string, string> LegacyColors = new Dictionary<string, string>();
                    LegacyColors.Add("PrimaryColorCode", "#CDBA8F");
                    LegacyColors.Add("AccentColorCode", "#116582");
                    LegacyColors.Add("TertiaryColorCode", "#F6E8BO");

                    if (contributionCohealerVm.IsCustomBrandingColorsActive)
                    {
                        if (contributionCohealerVm?.BrandingColors == null || contributionCohealerVm?.BrandingColors.Count == 0 || (contributionCohealerVm.BrandingColors.Count == LegacyColors.Count && !contributionCohealerVm.BrandingColors.Except(LegacyColors).Any()))
                        {
                            contributionCohealerVm.CoachSelectedBrandingColors = author?.BrandingColors;
                            contributionCohealerVm.CustomLogo = author?.CustomLogo;
                        }
                        else
                        {
                            contributionCohealerVm.CoachSelectedBrandingColors = contributionCohealerVm.BrandingColors;
                            contributionCohealerVm.CustomLogo = contributionCohealerVm.CustomLogo;
                        }
                    }
                    else
                    {
                        contributionCohealerVm.CoachSelectedBrandingColors = LegacyColors;
                        contributionCohealerVm.CustomLogo = null;
                    }
                    if (!contributionCohealerVm.CoachSelectedBrandingColors.ContainsKey("TextColorCode"))
                    {
                        contributionCohealerVm.CoachSelectedBrandingColors.Add("TextColorCode", "Auto");
                    }
                    //contributionCohealerVm.CoachSelectedBrandingColors = author?.BrandingColors;
                    contributionCohealerVm.IsCoachSelectedDarkModeEnabled = coachSelectedDarkMode;

                    var contributionPartners = await GetContributionPartnersAsync(contribution.Id);
                    if (contributionPartners.Succeeded)
                    {
                        contributionCohealerVm.ContributionPartners = contributionPartners.Payload;
                    }

                    if (!string.IsNullOrEmpty(author?.CountryId))
                    {
                        var availableCurrencies = await GetCurrenciesForContribution(author.CountryId);
                        contributionCohealerVm.AvailableCurrencies = availableCurrencies.ToList();
                    }

                    if (requesterUser.LastReadSocialInfos.ContainsKey(contribution.Id))
                    {
                        contributionCohealerVm.UnreadPostCount = await postsRepository.Count(x =>
                            x.ContributionId == contribution.Id 
                            && !x.IsDraft && x.CreateTime > requesterUser.LastReadSocialInfos[contribution.Id]);
                        
                    }
                    var latestDraftedPost = await postsRepository
                        .Get(m => m.ContributionId == contribution.Id && m.UserId == requesterUser.Id && m.IsDraft);

                    if (latestDraftedPost.Count() > 0)
                    {
                        contributionCohealerVm.LatestDraftPostId = latestDraftedPost.OrderByDescending(m => m.CreateTime).FirstOrDefault().Id;
                    }
                    

                    return OperationResult.Success(string.Empty, contributionCohealerVm);
                }

                return OperationResult.Forbid("It is not allowed to get the contribution that is not yours. To get contributions from other authors you must be logged in as a Client");
            }

            return OperationResult.Failure($"Contributions with the following Id is not found, Id: {id}");
        }
        private bool IsSessionToday(DateTime startDateTime)
        {
            return (startDateTime.Date == DateTime.UtcNow.Date);
        }

        public async Task<OperationResult> GetUpcomingCreatedByCohealerAsync(string userId, string fromJwtAccountId, string contributionType)
        {
            if (!ContributionTypes.Contains(contributionType))
            {
                return OperationResult.Failure($"Unsupported contribution type '{contributionType}'");
            }

            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == fromJwtAccountId);

            if (cohealerUser == null)
            {
                return OperationResult.Failure("Unable to find Cohealer by Id");
            }

            var isAllowedToRetrieve = cohealerUser.AccountId == fromJwtAccountId ||
                                      requestorAccount.Roles.Contains(Roles.Admin) ||
                                      requestorAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isAllowedToRetrieve)
            {
                return OperationResult.Failure("Forbidden to get all contributions created by other user ");
            }

            var upcomingContributions = await _contributionRootService.Get(c => c.UserId == cohealerUser.Id && c.Status != ContributionStatuses.Completed);
            var upcomingContributionsVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(upcomingContributions.Where(c => c.Type == contributionType));
            var contributionSessionsPairs = new Dictionary<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>>();

            var upcomingSessionList = new List<ClosestCohealerSession>();

            var closestClassForBanner = new ClosestClassForBannerViewModel();
            var initialValueMinutesLeft = int.MaxValue;
            closestClassForBanner.MinutesLeft = initialValueMinutesLeft;

            foreach (var upcomingContributionVm in upcomingContributionsVms)
            {
                var currentClosestClassForBanner = upcomingContributionVm.GetClosestCohealerClassForBanner(cohealerUser.TimeZoneId);
                upcomingContributionVm.ConvertAllOwnUtcTimesToZoned(cohealerUser?.TimeZoneId);

                if (currentClosestClassForBanner != null && currentClosestClassForBanner.MinutesLeft < closestClassForBanner.MinutesLeft)
                {
                    closestClassForBanner = currentClosestClassForBanner;
                }
                await FillPodsForSessionContribution(upcomingContributionVm);
                contributionSessionsPairs.Add(upcomingContributionVm, upcomingContributionVm.GetClosestCohealerSessions(true));
            }

            var isClosestTimeIsInitial = closestClassForBanner.MinutesLeft == initialValueMinutesLeft;
            if (!isClosestTimeIsInitial)
            {
                if (closestClassForBanner.ContributionType == nameof(ContributionOneToOne))
                {
                    var participantUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == closestClassForBanner.OneToOneParticipantId);
                    closestClassForBanner.Title = $"{participantUser.FirstName} {participantUser.LastName}";
                }
            }

            var clientDict = await GetOneToOneClientsDictionary(contributionSessionsPairs);

            foreach (var upcomingTimeSessionPair in contributionSessionsPairs)
            {
                var contributionVm = upcomingTimeSessionPair.Key;

                foreach (var upcomingSession in upcomingTimeSessionPair.Value)
                {
                    User client = null;
                    if (contributionVm is ContributionOneToOneViewModel)
                    {
                        var oneToOneClientId = upcomingSession.ParticipantsIds.FirstOrDefault();
                        client = oneToOneClientId != null
                            ? clientDict.GetValueOrDefault(oneToOneClientId)
                            : null;
                    }

                    var session = new ClosestCohealerSession
                    {
                        ContributionName = contributionVm.Title,
                        ContributionId = contributionVm.Id,
                        Title = client != null ? $"{client.FirstName} {client.LastName}" : upcomingSession?.Name == "Session" ? upcomingSession?.Title : upcomingSession?.Name,
                        StartTime = upcomingSession.StartTime,
                        EnrolledTotal = upcomingSession is null ? 0 : upcomingSession.EnrolledTotal,
                        EnrolledMax = upcomingSession is null ? 0 : upcomingSession.EnrolledMax,
                        ClassId = upcomingSession?.ClassId,
                        ClassGroupId = upcomingSession?.ClassGroupId,
                        TimezoneId = cohealerUser.TimeZoneId,
                        Type = contributionVm.Type,
                        ChatSid = upcomingSession?.ChatSid,
                        LiveVideoServiceProvider = contributionVm.LiveVideoServiceProvider,
                        ZoomStartUrl = upcomingSession?.ZoomStartMeeting,
                        IsPrerecorded = upcomingSession?.IsPrerecorded,
                        IsWorkshop = contributionVm.IsWorkshop
                    };

                    upcomingSessionList.Add(session);
                }
            }

            var contributions = await GetTabledContributionsAsync(cohealerUser, contributionSessionsPairs);

            var result = new GroupedTableContributionViewModel
            {
                ClosestClassForBanner = !isClosestTimeIsInitial ? closestClassForBanner : null,
                Type = contributionType,
                Contributions = contributions,
                UpcomingSessions = upcomingSessionList.OrderBy(s => s.StartTime)
            };

            return OperationResult.Success(null, result);
        }

        private async Task<Dictionary<string, User>> GetOneToOneClientsDictionary(
            Dictionary<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>> contributionSessionsPairs)
        {
            var clientsIds = contributionSessionsPairs.Where(e => e.Key is ContributionOneToOneViewModel)
                .SelectMany(e => e.Value)
                .Select(upcomingSession => upcomingSession.ParticipantsIds.First())
                .Distinct();

            var clients = await _unitOfWork.GetRepositoryAsync<User>().Get(e => clientsIds.Contains(e.Id));
            return clients.ToDictionary(e => e.Id);
        }

        public async Task<OperationResult> GetUpcomingCreatedByCohealerAsync(string userId, string fromJwtAccountId, int? skip, int? take, OrderByEnum orderByEnum)
        {
            int totalCount = 0;
            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var requesterAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == fromJwtAccountId);

            if (cohealerUser == null)
            {
                return OperationResult.Failure("Unable to find Cohealer by Id");
            }

            var isAllowedToRetrieve = cohealerUser.AccountId == fromJwtAccountId ||
                                      requesterAccount.Roles.Contains(Roles.Admin) ||
                                      requesterAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isAllowedToRetrieve)
            {
                return OperationResult.Failure("Forbidden to get all contributions created by other user");
            }
            IEnumerable<ContributionBase> upcomingContributions = Enumerable.Empty<ContributionBase>();
            if (skip != null && take != null)
            {
                upcomingContributions = await _contributionRootService.GetSkipTakeWithSort(c => c.UserId == cohealerUser.Id && c.Status != ContributionStatuses.Completed, Convert.ToInt32(skip), Convert.ToInt32(take), orderByEnum);
                totalCount = await _contributionRootService.GetCount(c => c.UserId == cohealerUser.Id && c.Status != ContributionStatuses.Completed);
                //totalCount = upcomingContributionsCount.Count();
            }
            else
            {
                upcomingContributions = await _contributionRootService.Get(c => c.UserId == cohealerUser.Id && c.Status != ContributionStatuses.Completed);
            }
            if (!upcomingContributions.Any())
            {
                return OperationResult.Success(string.Empty, new AllUpcomingContributionsForCohealer());
            }

            var upcomingContributionsVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(upcomingContributions);
            var contributionSessionsPairs = new Dictionary<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>>();

            var closestClassForBanner = new ClosestClassForBannerViewModel();
            var initialValueMinutesLeft = int.MaxValue;
            closestClassForBanner.MinutesLeft = initialValueMinutesLeft;

            foreach (var upcomingContributionVm in upcomingContributionsVms)
            {
                upcomingContributionVm.ConvertAllOwnUtcTimesToZoned(cohealerUser?.TimeZoneId);

                var currentClosestClassForBanner = upcomingContributionVm.GetClosestCohealerClassForBanner(cohealerUser.TimeZoneId);
                if (currentClosestClassForBanner != null && currentClosestClassForBanner.MinutesLeft < closestClassForBanner.MinutesLeft)
                {
                    closestClassForBanner = currentClosestClassForBanner;
                }
                await FillPodsForSessionContribution(upcomingContributionVm);
                contributionSessionsPairs.Add(upcomingContributionVm, upcomingContributionVm.GetClosestCohealerSessions(true));
            }

            var isClosestTimeIsInitial = closestClassForBanner.MinutesLeft == initialValueMinutesLeft;
            if (!isClosestTimeIsInitial)
            {
                if (closestClassForBanner.ContributionType == nameof(ContributionOneToOne))
                {
                    var participantUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == closestClassForBanner.OneToOneParticipantId);
                    closestClassForBanner.Title = $"{participantUser.FirstName} {participantUser.LastName}";
                }
            }

            var contributions = await GetTabledContributionsAsync(cohealerUser, contributionSessionsPairs);
            var allUpcomingContributions = new AllUpcomingContributionsForCohealer
            {
                ClosestClassForBanner = !isClosestTimeIsInitial ? closestClassForBanner : null,
                ContributionsForTable = contributions,
                AuthorAvatarUrl = cohealerUser.AvatarUrl
            };
            if (allUpcomingContributions.ContributionsForTable.Count() > 0)
            {
                allUpcomingContributions.TotalCount = totalCount;
            }
            return OperationResult.Success(null, allUpcomingContributions);
        }

        public async Task<OperationResult> GetArchivedCreatedByCohealerAsync(string userId, string fromJwtAccountId)
        {
            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == userId);
            var requestorAccount = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Id == fromJwtAccountId);

            if (cohealerUser == null)
            {
                return OperationResult.Failure("Unable to find Cohealer by Id");
            }

            var isAllowedToRetrieve = cohealerUser.AccountId == fromJwtAccountId ||
                                      requestorAccount.Roles.Contains(Roles.Admin) ||
                                      requestorAccount.Roles.Contains(Roles.SuperAdmin);

            if (!isAllowedToRetrieve)
            {
                return OperationResult.Failure("Forbidden to get all contributions created by other user ");
            }

            var upcomingContributions = (await _contributionRootService.Get(c => c.UserId == cohealerUser.Id && c.Status == ContributionStatuses.Completed)).ToList();
            var partnerContributions = (await _contributionRootService.Get(x => x.Partners.Any(y => y.IsAssigned && y.UserId == cohealerUser.Id) && x.Status == ContributionStatuses.Completed)).ToList();
            upcomingContributions.AddRange(partnerContributions);
            var upcomingContributionsVms = _mapper.Map<IEnumerable<ContributionBaseViewModel>>(upcomingContributions);
            var contributionSessionsPairs =
                upcomingContributionsVms.ToDictionary(c => c, c => new List<ClosestCohealerSessionInfo>());
            var contributions = await GetTabledContributionsAsync(cohealerUser, contributionSessionsPairs);

            return OperationResult.Success(null, contributions);
        }

        private async Task<List<ContribTableViewModel>> GetTabledContributionsAsync(
            User user,
            Dictionary<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>> contributionSessionsPairs)
        {
            var contributionList = new List<ContribTableViewModel>();
            var userTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == user.TimeZoneId);

            var oneToOneClientIds = contributionSessionsPairs.Where(csp => csp.Key is ContributionOneToOneViewModel).SelectMany(e => e.Value).SelectMany(e => e.ParticipantsIds).Distinct();
            var oneToOneClients = (await _unitOfWork.GetRepositoryAsync<User>().Get(e => oneToOneClientIds.Contains(e.Id))).ToDictionary(e => e.Id);
            //Code By Uzair
            //Rectified by Zia
            //End
            foreach (var upcomingTimeSessionPair in contributionSessionsPairs)
            {
                List<ClientModel> filteredUsers = new List<ClientModel>();
                var upcomingSession = upcomingTimeSessionPair.Value.FirstOrDefault();
                var contributionVm = upcomingTimeSessionPair.Key;
                var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributionId == contributionVm.Id);
                var postsRepository = _unitOfWork.GetRepositoryAsync<Post>();
                IEnumerable<User> users = Enumerable.Empty<User>();
                if (purchases.Count() > 0)
                {
                    var clientIds = purchases.Select(m => m.ClientId);
                    if (clientIds.Count() > 0)
                        users = await _unitOfWork.GetRepositoryAsync<User>().Get(p => clientIds.Contains(p.Id));
                }
                ClosestCohealerSession closestSession = null;
                if (upcomingSession != null)
                {
                    User client = null;
                    if (contributionVm is ContributionOneToOneViewModel)
                    {
                        var oneToOneClientId = upcomingSession.ParticipantsIds.First();
                        client = oneToOneClients.GetValueOrDefault(oneToOneClientId);
                    }

                    closestSession = new ClosestCohealerSession
                    {
                        ContributionName = contributionVm.Title,
                        ContributionId = contributionVm.Id,
                        Title = client != null ? $"{client.FirstName} {client.LastName}" : upcomingSession?.Name == "Session" ? upcomingSession?.Title : upcomingSession?.Name,
                        StartTime = upcomingSession.StartTime,
                        EnrolledTotal = upcomingSession is null ? 0 : upcomingSession.EnrolledTotal,
                        EnrolledMax = upcomingSession is null ? 0 : upcomingSession.EnrolledMax,
                        ClassId = upcomingSession?.ClassId,
                        ClassGroupId = upcomingSession?.ClassGroupId,
                        TimezoneId = user.TimeZoneId,
                        Type = contributionVm.Type,
                        ChatSid = upcomingSession?.ChatSid,
                        LiveVideoServiceProvider = contributionVm.LiveVideoServiceProvider,
                        ZoomStartUrl = upcomingSession?.ZoomStartMeeting,
                        IsPrerecorded = upcomingSession?.IsPrerecorded,
                        PreviewImageUrl = contributionVm.PreviewContentUrls.FirstOrDefault(),
                        SessionTimes = upcomingSession.SessionTimes,
                        PaymentType = contributionVm.PaymentType.ToString(),
                        IsCompleted = upcomingSession.IsCompleted,
                        GroupSessions = contributionVm is SessionBasedContributionViewModel sessionBasedCloset ? sessionBasedCloset?.Sessions.Where(m => m.Id == upcomingSession?.ClassGroupId).FirstOrDefault() : null,
                        OneToOneSessions = contributionVm is ContributionOneToOneViewModel oneToOneBased ? oneToOneBased?.AvailabilityTimes : null,
                    };
                }

                await FillPodsForSessionContribution(contributionVm);
                var contribTable = _mapper.Map<ContribTableViewModel>(contributionVm);
                if (contributionVm is SessionBasedContributionViewModel sessionBased)
                { 
                  contribTable.Sessions = sessionBased.Sessions.Where(m=> !m.IsCompleted).ToList();     
                }
                
                if (users.Count() > 0)
                {
                    contribTable.Clients = users.Select(u => new ClientModel { FirstName = u.FirstName, LastName = u.LastName, AvatarUrl = u.AvatarUrl }).ToList(); ;
                }
                //Code By Uzair
                // Rectified by Zia
                var count = (await _unitOfWork.GetRepositoryAsync<Purchase>().Get(prchase => prchase.ContributionId == contributionVm.Id)).Count();
                contribTable.StudentsNumber = count;
                //End
                contribTable.DeletingAllowed = count > 0 ? false : true;
                contribTable.ArchivingAllowed = contributionVm.ArchivingAllowed;

                contribTable.EarnedRevenue = await _cohealerIncomeService.GetContributionRevenueAsync(contributionVm.Id);
                contribTable.Symbol = contributionVm.DefaultSymbol?.ToUpper() ?? "$"; // By Uzair
                contribTable.Currency = contributionVm.DefaultCurrency?.ToUpper() ?? "USD"; // By Uzair
                contribTable.ClosestSession = closestSession;
                contribTable.PaymentType = contributionVm.PaymentType.ToString();
                //Extra Parameters
                contribTable.Participants = await GetParticipantsVmsAsync(contributionVm.Id);
                contribTable.ServiceProviderName = $"{user.FirstName} {user.LastName}";
                contribTable.AuthorAvatarUrl = user.AvatarUrl;
                contribTable.ContributionImage = contributionVm?.PreviewContentUrls?.FirstOrDefault();
                contribTable.IsMembersHiddenInCommunity = contributionVm.IsMembersHiddenInCommunity;
                contribTable.IsWorkshop = contributionVm.IsWorkshop;
                contribTable.IsInvoiced = contributionVm.IsInvoiced;
                contribTable.CreateTime = contributionVm.CreateTime;

                contribTable.Purpose= contributionVm.Purpose;
                if (user.LastReadSocialInfos.ContainsKey(contributionVm.Id))
                {
                    var postsCount = await postsRepository.Count(x =>
                        x.ContributionId == contributionVm.Id && !x.IsDraft && x.CreateTime > user.LastReadSocialInfos[contributionVm.Id]);
                    contribTable.UnReadPostCount = postsCount;
                }
                contribTable.CreateTime = contributionVm.CreateTime;
                contribTable.TimeZoneShortForm = userTimeZone?.ShortName;

                contributionList.Add(contribTable);
            }
            return contributionList?.OrderByDescending(m => m.CreateTime).ToList();
        }

        public async Task<OperationResult> ChangeStatusAsync(
            string contributionId,
            string adminAccountId,
            string accountId,
            AdminReviewNoteViewModel model)
        {
            var contribution =
                await _contributionRootService.GetOne(contributionId);

            if (contribution is null)
            {
                return OperationResult.Failure($"Contribution with following Id is not found: {contributionId}");
            }

            var adminUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == adminAccountId);

            if (adminUser is null)
            {
                return OperationResult.Failure($"User with following accountId is not found: {adminAccountId}");
            }

            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);

            if (user is null)
            {
                return OperationResult.Failure($"User with following accountId is not found: {accountId}");
            }

            if (contribution.Status is ContributionStatuses.Approved ||
                contribution.Status is ContributionStatuses.Rejected)
            {
                return OperationResult.Failure($"Unable to change '{contribution.Status}' contribution status");
            }

            var reviewNote = _mapper.Map<AdminReviewNote>(model);
            reviewNote.DateUtc = DateTime.UtcNow;
            reviewNote.UserId = adminUser.Id;

            contribution.Status = reviewNote.Status;
            contribution.AdminReviewNotes.Add(reviewNote);

            if (contribution.Status == ContributionStatuses.Approved)
            {
                if(!contribution.IsGroupChatHidden)
                {
                    var chatCreationResult = await _chatService.CreateChatForContribution(contribution);
                    if (chatCreationResult.Failed)
                    {
                        //return OperationResult.Failure(@$"Unable to create required for every contribution chat due to error: {chatCreationResult.Message}");
                    }
                    else
                        contribution.Chat = chatCreationResult.Payload;
                }
              

                if (contribution is SessionBasedContribution contributionCourse)
                {
                    foreach (var session in contributionCourse.Sessions)
                    {
                        if (session.IsPrerecorded)
                        {
                            foreach (var sessionTime in session.SessionTimes)
                            {
                                var note = new Note
                                {
                                    UserId = contribution.UserId,
                                    ClassId = session.Id,
                                    ContributionId = contributionId,
                                    Title = session.Title,
                                    SubClassId = sessionTime.Id,
                                    IsPrerecorded = true

                                };
                                await _unitOfWork.GetRepositoryAsync<Note>().Insert(note);
                            }
                        }
                        else
                        {
                            var note = new Note
                            {
                                UserId = contribution.UserId,
                                ClassId = session.Id,
                                ContributionId = contributionId,
                                Title = session.Title,
                            };
                            await _unitOfWork.GetRepositoryAsync<Note>().Insert(note);
                        }

                    }
                }

                var currentPaidTier = await _paidTiersService.GetCurrentPaidTier(user.AccountId);

                if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments))
                {
                    if (contribution.PaymentType == PaymentTypes.Advance)
                    {
                        if (!user.IsStandardAccount)
                        {
                            return OperationResult.Failure("Advance payment is only for standard account users");
                        }
                        var createAdvanceBillingPlanResult = await AddContributionAssociatedSplitPaymentStripeProductPlanForAdvancePay(contribution, currentPaidTier.PaidTierOption, user.StripeStandardAccountId);

                        if (createAdvanceBillingPlanResult.Failed)
                        {
                            return createAdvanceBillingPlanResult;
                        }

                        contribution.PaymentInfo.BillingPlanInfo = createAdvanceBillingPlanResult.Payload;
                    }
                    else
                    {
                        var createBillingPlanResult = await AddContributionAssociatedSplitPaymentStripeProductPlan(contribution, currentPaidTier.PaidTierOption);

                        if (createBillingPlanResult.Failed)
                        {
                            return createBillingPlanResult;
                        }
                        contribution.PaymentInfo.BillingPlanInfo = createBillingPlanResult.Payload;
                    }
                }

                if (contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
                {
                    string stripeStandardAccountId = string.Empty;
                    if (contribution.PaymentType == PaymentTypes.Advance && user.IsStandardAccount)
                    {
                        stripeStandardAccountId = user.StripeStandardAccountId;
                    }

                    //advance payment also handled in the fucntion
                    var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
                    var createBillingPlanResult = await AddContributionAssociatedMonthlySessionSubscriptionStripeProductPlan(contribution, currentPaidTier.PaidTierOption, stripeStandardAccountId, contributorUser.CountryId);
                    if (createBillingPlanResult.Failed)
                    {
                        return createBillingPlanResult;
                    }
                    contribution.PaymentInfo.BillingPlanInfo = createBillingPlanResult.Payload;
                    contribution.PaymentInfo.BillingPlanInfo = createBillingPlanResult.Payload;
                    var contributionOneToOne = contribution as ContributionOneToOne;
                    contributionOneToOne.CoachStandardAccountId = stripeStandardAccountId;
                    await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);
                }

                if (contribution is ContributionMembership || contribution is ContributionCommunity)
                {
                    bool isCommunityFreeOnly = contribution is ContributionCommunity &&
                        contribution.PaymentInfo.PaymentOptions?.Count() == 1 &&
                        contribution.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free);
                    if (contribution.PaymentInfo.MembershipInfo.ProductBillingPlans is null ||
                        contribution.PaymentInfo.MembershipInfo.ProductBillingPlans.Count == 0 &&
                        !isCommunityFreeOnly)
                    {
                        string stripeStandardAccountId = string.Empty;
                        if (contribution.PaymentType == PaymentTypes.Advance && user.IsStandardAccount)
                        {
                            stripeStandardAccountId = user.StripeStandardAccountId;
                        }
                        //advance payment also handled in the fucntion
                        var productPlanCreatingResult =
                             await AddContributionAssociatedSessionBaseStripeProductPlan((SessionBasedContribution)contribution, currentPaidTier.PaidTierOption, stripeStandardAccountId);

                        if (!productPlanCreatingResult.Succeeded)
                        {
                            return productPlanCreatingResult;
                        }

                        contribution.PaymentInfo.MembershipInfo.ProductBillingPlans = productPlanCreatingResult.Payload;
                    }
                }
            }

            ContributionBase updatedContribution;
            try
            {
                updatedContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contribution);
            }
            catch (Exception ex)
            {
                if (contribution.Status == ContributionStatuses.Approved && contribution is SessionBasedContribution)
                {
                    var chatDeletionResult = await _chatService.DeleteChatForContribution(contribution.Chat.Sid);
                    _logger.Log(LogLevel.Error, $"Unable to update contribution with title {contribution.Title} to DB. Exception: {ex.Message}. Chat {contribution.Chat.Sid} deletion succeeded: {chatDeletionResult.Succeeded} with message {chatDeletionResult.Message}");
                }

                return OperationResult.Failure("Error occured during contribution update, try again later and if the problem persists contact support");
            }

            if (reviewNote.Status == ContributionStatuses.Approved)
            {
                try
                {
                    var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.Id == contribution.UserId);
                    coachUser.IsFirstAcceptedCourseExists = true;
                    await _unitOfWork.GetRepositoryAsync<User>().Update(coachUser.Id, coachUser);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error during updating user");
                }
            }

            await _notifictionService.SendContributionStatusNotificationToAuthor(contribution);
            // handled in active campaign
            //await _notifictionService.SendEmailCohealerShareContributionGuide(contribution);

            return OperationResult.Success(null, _mapper.Map<ContributionBaseViewModel>(updatedContribution));
        }

        public async Task<OperationResult> ShareContribution(ShareContributionEmailViewModel shareContributionVm, string inviterAccountId)
        {
            var contributionToShare = await _contributionRootService.GetOne(shareContributionVm.ContributionId);
            await _notifictionService.SendContributionInvitationMessage(contributionToShare, shareContributionVm.EmailAddresses, inviterAccountId);

            return OperationResult.Success("Invitation message(s) have been sent");
        }

        public async Task<GroupedAdminContributionsViewModel> GetAllContributionsForAdminAsync(string adminAccountId)
        {
            var adminUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == adminAccountId);

            var contributionsForAdmin = await _contributionRootService.Get(c => c.Status == ContributionStatuses.InReview
                          || c.Status == ContributionStatuses.Revised
                          || c.Status == ContributionStatuses.Approved
                          || c.Status == ContributionStatuses.Rejected);

            if (contributionsForAdmin != null)
            {
                var contributionForAdminAuthorIds = contributionsForAdmin.Select(c => c.UserId);

                var authors = await _unitOfWork.GetRepositoryAsync<User>().Get(u => contributionForAdminAuthorIds.Contains(u.Id));

                var contributionsForAdminBrief = _mapper.Map<IEnumerable<ContributionAdminBriefViewModel>>(contributionsForAdmin).ToList();

                contributionsForAdminBrief.ForEach(c =>
                {
                    var author = authors.FirstOrDefault(a => a.Id == c.UserId);
                    c.ServiceProviderName = author is null ? "deleted user" : $"{author.FirstName} {author.LastName}";
                    c.CreateTime = DateTimeHelper.GetZonedDateTimeFromUtc(c.CreateTime, adminUser.TimeZoneId);
                    c.UpdateTime = DateTimeHelper.GetZonedDateTimeFromUtc(c.UpdateTime, adminUser.TimeZoneId);
                    c.TimeZoneId = adminUser.TimeZoneId;
                });

                var groupedContributionsViewModel = new GroupedAdminContributionsViewModel();
                groupedContributionsViewModel.Approved = contributionsForAdminBrief
                    .Where(c => c.Status == ContributionStatuses.Approved.ToString()).ToList();
                groupedContributionsViewModel.Review = contributionsForAdminBrief
                    .Where(c => c.Status == ContributionStatuses.InReview.ToString()).ToList();
                groupedContributionsViewModel.Updated = contributionsForAdminBrief
                    .Where(c => c.Status == ContributionStatuses.Revised.ToString()).ToList();
                groupedContributionsViewModel.Rejected = contributionsForAdminBrief
                    .Where(c => c.Status == ContributionStatuses.Rejected.ToString()).ToList();

                if (groupedContributionsViewModel.Approved.Any())
                {
                    groupedContributionsViewModel.Approved = groupedContributionsViewModel.Approved.OrderByDescending(c => c.UpdateTime);
                }

                if (groupedContributionsViewModel.Review.Any())
                {
                    groupedContributionsViewModel.Review = groupedContributionsViewModel.Review.OrderByDescending(c => c.UpdateTime);
                }

                if (groupedContributionsViewModel.Updated.Any())
                {
                    groupedContributionsViewModel.Updated = groupedContributionsViewModel.Updated.OrderByDescending(c => c.UpdateTime);
                }

                if (groupedContributionsViewModel.Rejected.Any())
                {
                    groupedContributionsViewModel.Rejected = groupedContributionsViewModel.Rejected.OrderByDescending(c => c.UpdateTime);
                }

                return groupedContributionsViewModel;
            }

            return null;
        }

        public async Task<DashboardContributionsViewModel> GetDashboardContributionsForCohealerAsync(string accountId)
        {
            var cohealer = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var cohealerTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == cohealer.TimeZoneId);


            var upcomingContributions = await _contributionRootService.Get(c => c.UserId == cohealer.Id && c.Status == ContributionStatuses.Approved);

            var upcomingContributionsVms = _mapper.Map<IList<ContributionBaseViewModel>>(upcomingContributions);

            var closestClassForBanner = await GetClosestClassForBanner(upcomingContributionsVms);

            var upcomingSessions = new List<KeyValuePair<ContributionBaseViewModel, ClosestCohealerSessionInfo>>();


            foreach (var upcomingContributionVm in upcomingContributionsVms)
            {
                var currentClosestClassForBanner = upcomingContributionVm.GetClosestCohealerClassForBanner(cohealer.TimeZoneId);
                upcomingContributionVm.ConvertAllOwnUtcTimesToZoned(cohealer?.TimeZoneId);

                await FillPodsForSessionContribution(upcomingContributionVm);

                var contributionClosestSessions = upcomingContributionVm.GetClosestCohealerSessions(true);
                // filter out self paced sessions
                contributionClosestSessions = contributionClosestSessions.Where(s => !s.IsPrerecorded)?.ToList();
                contributionClosestSessions.ForEach(si =>
                    upcomingSessions.Add(new KeyValuePair<ContributionBaseViewModel, ClosestCohealerSessionInfo>(upcomingContributionVm, si)));
            }

            var threeContributionsForDashboard = new List<ContributionOnDashboardViewModel>();

            var upcomingSessionsForDashboard = upcomingSessions
                .OrderBy(s => s.Value.StartTime)
                .Take(Constants.CohealerDashboardContributionsCount);

            foreach (var upcomingTimeSessionPair in upcomingSessionsForDashboard)
            {
                var upcomingSession = upcomingTimeSessionPair.Value;
                var contributionVm = upcomingTimeSessionPair.Key;

               
                User client = null;
                if (contributionVm is ContributionOneToOneViewModel)
                {
                    var oneToOneClientId = upcomingSession.ParticipantsIds.First();
                    client = oneToOneClientId != null
                        ? await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == oneToOneClientId)
                        : null;
                    var session = ((ContributionOneToOneViewModel)contributionVm).AvailabilityTimes.Where(a => a.Id == upcomingSession.ClassGroupId).FirstOrDefault()?.BookedTimes.Where(a=> a.Id == upcomingSession.ClassId).FirstOrDefault();
                    if (session != null)
                    {
                        upcomingSession.ZoomStartMeeting = session.ZoomMeetingData?.StartUrl;
                        if (closestClassForBanner?.ClassId == session.Id)
                            closestClassForBanner.ZoomStartUrl = upcomingSession.ZoomStartMeeting;
                    }
                }

                threeContributionsForDashboard.Add(new ContributionOnDashboardViewModel
                {
                    Id = contributionVm.Id,
                    UserId = contributionVm.UserId,
                    Title = contributionVm.Title,
                    Type = contributionVm.Type,
                    ContributionImage = contributionVm.PreviewContentUrls?.FirstOrDefault(),
                    TimeZoneShortForm = cohealerTimeZone?.ShortName,
                    ClosestSession = new ClosestCohealerSession
                    {
                        Type = contributionVm.Type,
                        Title = client != null ? $"{client.FirstName} {client.LastName}" : upcomingSession?.Name == "Session" ? upcomingSession?.Title : upcomingSession?.Name,
                        StartTime = upcomingSession.StartTime,
                        EnrolledTotal = upcomingSession is null ? 0 : upcomingSession.EnrolledTotal,
                        EnrolledMax = upcomingSession is null ? 0 : upcomingSession.EnrolledMax,
                        ClassId = upcomingSession?.ClassId,
                        ClassGroupId = upcomingSession?.ClassGroupId,
                        ChatSid = upcomingSession?.ChatSid,
                        ContributionId = contributionVm.Id,
                        ContributionName = contributionVm.Title,
                        LiveVideoServiceProvider = contributionVm.LiveVideoServiceProvider,
                        ZoomStartUrl = upcomingSession?.ZoomStartMeeting,
                        IsPrerecorded = upcomingSession?.IsPrerecorded,
                        SessionTimes = upcomingSession.SessionTimes,
                        IsCompleted = upcomingSession.IsCompleted,
                        TimeZone = cohealer?.TimeZoneId,
                        GroupSessions = contributionVm is SessionBasedContributionViewModel sessionBasedCloset ? sessionBasedCloset?.Sessions?.Where(m => m.Id == upcomingSession?.ClassGroupId).FirstOrDefault() : null,
                        OneToOneSessions = contributionVm is ContributionOneToOneViewModel oneToOneBased ? oneToOneBased?.AvailabilityTimes : null,
                    },
                    Sessions = contributionVm is SessionBasedContributionViewModel sessionBased ? sessionBased.Sessions : new List<Session>()

                });
            }

            var declinedContributions = await ListCoachIncompleteSubscriptions(cohealer.AccountId);

            return new DashboardContributionsViewModel
            {
                ContributionsForDashboard = threeContributionsForDashboard,
                ClosestClassForBanner = closestClassForBanner,
                CoachDeclinedSubscriptions = declinedContributions
            };
        }
        public async Task<DashboardContributionsViewModel> GetAllSessionsForCohealer(string accountId, bool isPartner, int? skip, int? take, string type = null, List<ContributionStatuses> contributionStatuses = null)
        {

            var cohealer = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var contributions = Enumerable.Empty<ContributionBase>();
            if (isPartner)
            {
                var availablePartnerContributionTypes = string.IsNullOrEmpty(type)
               ? new List<string>() { nameof(ContributionCourse), nameof(ContributionMembership), nameof(ContributionCommunity) }
               : new List<string>() { type };

                contributions = (await _contributionRootService.Get(x => x.Partners.Any(y => y.IsAssigned && y.UserId == cohealer.Id)))
                    .Where(x => availablePartnerContributionTypes.Contains(x.Type) && !x.Status.Equals(ContributionStatuses.Completed));
            }
            else
            {
                 contributions = await _contributionRootService.GetSkipTake(c => c.UserId == cohealer.Id && c.Status == ContributionStatuses.Approved, Convert.ToInt32(skip), Convert.ToInt32(take));
            }
            var upcomingContributionsVms = _mapper.Map<IList<ContributionBaseViewModel>>(contributions);

            var closestClassForBanner = await GetClosestClassForBanner(upcomingContributionsVms);

            var upcomingSessions = new List<KeyValuePair<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>>>();

            foreach (var upcomingContributionVm in upcomingContributionsVms)
            {
                var currentClosestClassForBanner = upcomingContributionVm.GetClosestCohealerClassForBanner(cohealer.TimeZoneId);
                upcomingContributionVm.ConvertAllOwnUtcTimesToZoned(cohealer?.TimeZoneId);

                await FillPodsForSessionContribution(upcomingContributionVm);

                var contributionClosestSessions = upcomingContributionVm.GetCohealerSessions();
                contributionClosestSessions = contributionClosestSessions.Where(s => !s.IsPrerecorded)?.ToList();
                if (contributionClosestSessions.Count > 0)
                {
                if(contributionClosestSessions.Count > 0)
                {
                    upcomingSessions.Add(new KeyValuePair<ContributionBaseViewModel, List<ClosestCohealerSessionInfo>>(upcomingContributionVm, contributionClosestSessions));
                }
            }

            }

            var threeContributionsForDashboard = new List<ContributionOnDashboardViewModel>();

            foreach (var upcomingTimeSessionPair in upcomingSessions)
            {
                var upcomingSessionsArray = upcomingTimeSessionPair.Value;
                var contributionVm = upcomingTimeSessionPair.Key;
                if (!upcomingSessionsArray.Any())
                {
                    threeContributionsForDashboard.Add(new ContributionOnDashboardViewModel
                    {
                        Id = contributionVm.Id,
                        UserId = contributionVm.UserId,
                        Title = contributionVm.Title,
                        Type = contributionVm.Type,
                        ContributionImage = contributionVm.PreviewContentUrls?.FirstOrDefault(),
                        ClosestSession = null,
                        Sessions = new List<Session>()
                    });
                }
                foreach(var upcomingSession in upcomingSessionsArray)
                {
                    User client = null;
                    if (contributionVm is ContributionOneToOneViewModel)
                    {
                        var oneToOneClientId = upcomingSession.ParticipantsIds.First();
                        client = oneToOneClientId != null
                            ? await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == oneToOneClientId)
                            : null;
                        var session = ((ContributionOneToOneViewModel)contributionVm).AvailabilityTimes.Where(a => a.Id == upcomingSession.ClassGroupId).FirstOrDefault()?.BookedTimes.Where(a => a.Id == upcomingSession.ClassId).FirstOrDefault();
                        if (session != null)
                        {
                            upcomingSession.ZoomStartMeeting = session.ZoomMeetingData?.StartUrl;
                            if (closestClassForBanner?.ClassId == session.Id)
                                closestClassForBanner.ZoomStartUrl = upcomingSession.ZoomStartMeeting;
                        }
                    }

                    threeContributionsForDashboard.Add(new ContributionOnDashboardViewModel
                    {
                        Id = contributionVm.Id,
                        UserId = contributionVm.UserId,
                        Title = contributionVm.Title,
                        Type = contributionVm.Type,
                        ContributionImage = contributionVm.PreviewContentUrls?.FirstOrDefault(),
                        ClosestSession = new ClosestCohealerSession
                        {
                            Type = contributionVm.Type,
                            Title = client != null ? $"{client.FirstName} {client.LastName}" : upcomingSession?.Name == "Session" ? upcomingSession?.Title : upcomingSession?.Name,
                            StartTime = upcomingSession.StartTime,
                            EnrolledTotal = upcomingSession is null ? 0 : upcomingSession.EnrolledTotal,
                            EnrolledMax = upcomingSession is null ? 0 : upcomingSession.EnrolledMax,
                            ClassId = upcomingSession?.ClassId,
                            ClassGroupId = upcomingSession?.ClassGroupId,
                            ChatSid = upcomingSession?.ChatSid,
                            ContributionId = contributionVm.Id,
                            ContributionName = contributionVm.Title,
                            LiveVideoServiceProvider = contributionVm.LiveVideoServiceProvider,
                            ZoomStartUrl = upcomingSession?.ZoomStartMeeting,
                            IsPrerecorded = upcomingSession?.IsPrerecorded,
                            SessionTimes = upcomingSession.SessionTimes,
                            IsCompleted = upcomingSession.IsCompleted,
                            TimeZone = cohealer?.TimeZoneId,
                            GroupSessions = contributionVm is SessionBasedContributionViewModel sessionBasedCloset ? sessionBasedCloset?.Sessions?.Where(m => m.Id == upcomingSession?.ClassGroupId).FirstOrDefault() : null,
                            OneToOneSessions = contributionVm is ContributionOneToOneViewModel oneToOneBased ? oneToOneBased?.AvailabilityTimes : null,
                        },
                        Sessions = contributionVm is SessionBasedContributionViewModel sessionBased ? sessionBased.Sessions : new List<Session>()

                    });
                }
                
            }

            var declinedContributions = await ListCoachIncompleteSubscriptions(cohealer.AccountId);

            return new DashboardContributionsViewModel
            {
                ContributionsForDashboard = threeContributionsForDashboard,
                ClosestClassForBanner = closestClassForBanner,
                CoachDeclinedSubscriptions = declinedContributions
            };
        }
        public async void GetActiveCampaignResult(string accountId)
        {
            try
            {
                var account = await _unitOfWork.GetGenericRepositoryAsync<Account>().GetOne(m=>m.Id == accountId);
                if (!string.IsNullOrEmpty(account.Email))
                {
                    if (await _activeCampaignService.IsContactExistsAndHaveDeal(account.Email))
                    {
                        ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
                        ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
                        {
                            CohereAccountId = accountId,
                            LastCohereActivity = DateTime.UtcNow.ToString("MM/dd/yyyy"),

                        };
                        _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send active campaign last activity data");
            }

        }
        private async Task<ClosestClassForBannerViewModel> GetClosestClassForBanner(IList<ContributionBaseViewModel> upcomingContributionsVms)
        {
            var coachesIds = upcomingContributionsVms.Select(e => e.UserId);
            var coachesTimezones = (await _unitOfWork.GetRepositoryAsync<User>().Get(e => coachesIds.Contains(e.Id))).ToDictionary(e => e.Id, e => e.TimeZoneId);

            var closestClassForBanner = new ClosestClassForBannerViewModel();
            var initialValueMinutesLeft = int.MaxValue;
            closestClassForBanner.MinutesLeft = initialValueMinutesLeft;

            foreach (var upcomingContributionVm in upcomingContributionsVms)
            {
                var currentClosestClassForBanner = upcomingContributionVm.GetClosestCohealerClassForBanner(coachesTimezones[upcomingContributionVm.UserId]);
                if (currentClosestClassForBanner != null && currentClosestClassForBanner.MinutesLeft < closestClassForBanner.MinutesLeft)
                {
                    closestClassForBanner = currentClosestClassForBanner;
                }
            }

            var isClosestTimeIsInitial = closestClassForBanner.MinutesLeft == initialValueMinutesLeft;
            if (!isClosestTimeIsInitial)
            {
                if (closestClassForBanner.ContributionType == nameof(ContributionOneToOne))
                {
                    var participantUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == closestClassForBanner.OneToOneParticipantId);
                    closestClassForBanner.Title = $"{participantUser.FirstName} {participantUser.LastName}";
                }
            }

            return !isClosestTimeIsInitial ? closestClassForBanner : null;
        }

        private async Task<OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>> AddContributionAssociatedSessionBaseStripeProductPlan(
            SessionBasedContribution contribution,
            PaidTierOption paidTierOption,
            string standardAccountId = null)
        {
            if (!IsPaymentOptionsValid(contribution))
            {
                return OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>.Failure("not allowed payment option");
            }

            if (!IsPricesValid(contribution))
            {
                return OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>.Failure("missed prices info");
            }

            string productId = null;
            var getProductPlanOperationResult = await _stripeService.GetProductAsync(contribution.Id, standardAccountId);
            if (getProductPlanOperationResult?.Payload?.Id != contribution.Id)
            {
                var createProductPlanOperationResult = await _stripeService.CreateProductAsync(new CreateProductViewModel()
                {
                    Id = contribution.Id,
                    Name = contribution.Title,
                    StandardAccountId = standardAccountId
                });
                if (createProductPlanOperationResult.Failed)
                {
                    return OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>.Failure(createProductPlanOperationResult.Message);
                }
                else
                {
                    productId = createProductPlanOperationResult.Payload;
                }
            }
            else
            {
                productId = getProductPlanOperationResult.Payload.Id;
            }

            var result = new Dictionary<PaymentOptions, BillingPlanInfo>();

            var notLimitedPaymentOptions = contribution.PaymentInfo.PaymentOptions
                .Except(new[] { PaymentOptions.MembershipPackage, PaymentOptions.Free });

            var coachUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);

            foreach (var paymentOption in notLimitedPaymentOptions)
            {
                var billingPlan = await CreateNotLimitedPlan(contribution, paymentOption, paidTierOption, coachUser.CountryId, standardAccountId);

                if (billingPlan.Failed)
                {
                    return OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>.Failure(billingPlan.Message);
                }

                result.Add(paymentOption, billingPlan.Payload);
            }

            return OperationResult<Dictionary<PaymentOptions, BillingPlanInfo>>.Success(result);

            async Task<OperationResult<BillingPlanInfo>> CreateNotLimitedPlan(ContributionBase contribution, PaymentOptions paymentOption, PaidTierOption paidTierOption, string countryId, string standardAccountId = null)
            {
                var stripeInterval = StripePlanIntervalByPaymentOptions[paymentOption];

                var pricePerInterval = contribution.PaymentInfo.MembershipInfo.Costs[paymentOption]
                                       * _stripeService.SmallestCurrencyUnit;

                var planGrossAmountBill = _paymentSystemFeeService.CalculateGrossAmountAsLong(
                    pricePerInterval, contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId);

                string planId = string.Empty;

                //in case of advance payment create the Recurring taxable price on product instead of plan
                if (contribution.PaymentType == PaymentTypes.Advance && !string.IsNullOrEmpty(standardAccountId))
                {
                    var metadata = new Dictionary<string, string>()
                    {
                        {Constants.Stripe.MetadataKeys.PaymentOption, stripeInterval}
                    };

                    var createPlan = await _stripeService.GetPriceForProductRecurringPaymentAsync(productId, stripeInterval, standardAccountId) ??
                        await _stripeService.CreateTaxableRecurringPriceForProduct(productId, planGrossAmountBill, contribution.DefaultCurrency, stripeInterval, intervalCount: 1,
                        standardAccountId, contribution.TaxType, metadata);

                    if (string.IsNullOrEmpty(createPlan))
                    {
                        return OperationResult<BillingPlanInfo>.Failure($"Error during creating the price plane for product {contribution.Id}");
                    }
                    planId = createPlan;
                }
                else
                {
                    var createModel = new CreateProductPlanViewModel
                    {
                        ProductId = productId,
                        Amount = planGrossAmountBill,
                        Interval = stripeInterval,
                        Name = $"Membership plan ({stripeInterval})"
                    };

                    var createPlanResult = await _stripeService.CreateProductPlanAsync(createModel, contribution.DefaultCurrency);

                    if (createPlanResult.Failed)
                    {
                        return OperationResult<BillingPlanInfo>.Failure(createPlanResult.Message);
                    }
                    planId = createPlanResult.Payload;
                }

                var serviceProviderIncomePerInterval = _pricingeCalculationService.CalculateServiceProviderIncome
                    (pricePerInterval, contribution.PaymentInfo.CoachPaysStripeFee, paidTierOption.NormalizedFee, contribution.PaymentType, countryId).Total;

                var billingPlanGrossCost = planGrossAmountBill / _stripeService.SmallestCurrencyUnit;
                var billingPlanPureCost = pricePerInterval / _stripeService.SmallestCurrencyUnit;
                var billingPlanTransferCost = serviceProviderIncomePerInterval / _stripeService.SmallestCurrencyUnit;

                return OperationResult<BillingPlanInfo>.Success(new BillingPlanInfo
                {
                    ProductBillingPlanId = planId,
                    BillingPlanGrossCost = billingPlanGrossCost,
                    BillingPlanPureCost = billingPlanPureCost,
                    BillingPlanTransferCost = billingPlanTransferCost
                });
            }

            static bool IsPaymentOptionsValid(SessionBasedContribution contributionMembership)
            {
                var allowedPaymentOptions = new[]
                {
                    PaymentOptions.DailyMembership,
                    PaymentOptions.MonthlyMembership,
                    PaymentOptions.WeeklyMembership,
                    PaymentOptions.YearlyMembership,
                    PaymentOptions.MembershipPackage,
                    PaymentOptions.Free
                };
                return contributionMembership.PaymentInfo.PaymentOptions.All(po => allowedPaymentOptions.Contains(po));
            }

            static bool IsPricesValid(SessionBasedContribution contributionMembership)
            {
                return contributionMembership.PaymentInfo.PaymentOptions
                    .Where(po => po != PaymentOptions.Free)
                    .All(po =>
                    contributionMembership.PaymentInfo.MembershipInfo.Costs.ContainsKey(po));
            }
        }

        private async Task<OperationResult<BillingPlanInfo>> AddContributionAssociatedSplitPaymentStripeProductPlan(
            ContributionBase contribution,
            PaidTierOption paidTierOption)
        {
            if (!contribution.PaymentInfo.SplitNumbers.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution split numbers are not specified");
            }

            if (!contribution.PaymentInfo.SplitPeriod.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution split period is not specified");
            }

            if (!contribution.PaymentInfo.Cost.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution session cost is not specified");
            }

            var productCreatingResult = await _stripeService.CreateProductAsync(
                new CreateProductViewModel { Id = contribution.Id, Name = contribution.Title });

            if (productCreatingResult.Failed)
            {
                return OperationResult<BillingPlanInfo>.Failure(productCreatingResult.Message);
            }

            if (!StripePlanIntervals.TryGetValue(contribution.PaymentInfo.SplitPeriod.Value, out var stripeInterval))
            {
                return OperationResult<BillingPlanInfo>.Failure(
                    $"Unsupported '{contribution.PaymentInfo.SplitPeriod}' {nameof(contribution.PaymentInfo.SplitPeriod)} type.");
            }

            var productId = productCreatingResult.Payload;
            var billingPlanPureAmount = _pricingeCalculationService.CalculateBillingPlanCost(
                contribution.PaymentInfo.Cost.Value * _stripeService.SmallestCurrencyUnit,
                contribution.PaymentInfo.SplitNumbers.Value);
            var planGrossAmountBill = _paymentSystemFeeService.CalculateGrossAmountAsLong(billingPlanPureAmount, contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId);
            var paymentInfo = contribution.PaymentInfo;

            var createModel = new CreateProductPlanViewModel
            {
                ProductId = productId,
                Amount = planGrossAmountBill,
                Interval = stripeInterval,
                SplitNumbers = paymentInfo.SplitNumbers.Value,
                Name = paymentInfo.SplitPeriod.ToString()
            };

            var result = await _stripeService.CreateProductPlanAsync(createModel, contribution.DefaultCurrency);

            if (result.Failed)
            {
                return OperationResult<BillingPlanInfo>.Failure(result.Message);
            }

            var coachUser = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            var billingPlanId = result.Payload;
            var billingPlanGrossCost = planGrossAmountBill / _stripeService.SmallestCurrencyUnit;
            var billingPlanTransferCost = _pricingeCalculationService.CalculateServiceProviderIncome
            (billingPlanPureAmount, contribution.PaymentInfo.CoachPaysStripeFee, paidTierOption.NormalizedFee, contribution.PaymentType, coachUser.CountryId).Total;

            var billingPlanInfo = new BillingPlanInfo
            {
                ProductBillingPlanId = billingPlanId,
                BillingPlanGrossCost = billingPlanGrossCost,
                BillingPlanPureCost = billingPlanPureAmount / _stripeService.SmallestCurrencyUnit,
                BillingPlanTransferCost = billingPlanTransferCost / _stripeService.SmallestCurrencyUnit,
                TotalBillingGrossCost = billingPlanGrossCost * paymentInfo.SplitNumbers.Value,
                TotalBillingPureCost = paymentInfo.Cost.Value
            };

            return OperationResult<BillingPlanInfo>.Success(null, billingPlanInfo);
        }

        private async Task<OperationResult<BillingPlanInfo>> AddContributionAssociatedSplitPaymentStripeProductPlanForAdvancePay(
            ContributionBase contribution,
            PaidTierOption paidTierOption,
            string standardAccountId)
        {
            if (!contribution.PaymentInfo.SplitNumbers.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution split numbers are not specified");
            }
            if (!contribution.PaymentInfo.SplitPeriod.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution split period is not specified");
            }
            if (!contribution.PaymentInfo.Cost.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure("Contribution session cost is not specified");
            }
            if (!StripePlanIntervals.TryGetValue(contribution.PaymentInfo.SplitPeriod.Value, out var stripeInterval))
            {
                return OperationResult<BillingPlanInfo>.Failure(
                    $"Unsupported '{contribution.PaymentInfo.SplitPeriod}' {nameof(contribution.PaymentInfo.SplitPeriod)} type.");
            }
            //billing and pricing info
            var billingPlanPureAmount = _pricingeCalculationService.CalculateBillingPlanCost(
                contribution.PaymentInfo.Cost.Value * _stripeService.SmallestCurrencyUnit,
                contribution.PaymentInfo.SplitNumbers.Value);
            var planGrossAmountBill = _paymentSystemFeeService.CalculateGrossAmountAsLong(billingPlanPureAmount, contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId);
            var paymentInfo = contribution.PaymentInfo;
            var createModel = new CreateProductWithTaxblePlaneViewModel
            {
                Id = contribution.Id,
                Name = contribution.Title,
                Interval = stripeInterval,
                TaxType = contribution.TaxType,
                StandardAccountId = standardAccountId,
                Amount = planGrossAmountBill,
                SplitNumbers = paymentInfo.SplitNumbers.Value,
                Currency = contribution.DefaultCurrency,
            };
            var productPlanResult = await _stripeService.CreateProductWithTaxablePlanAsync(createModel);
            if (productPlanResult.Failed)
            {
                return OperationResult<BillingPlanInfo>.Failure(productPlanResult.Message);
            }

            var coachUser = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == contribution.UserId);
            var billingPlanGrossCost = planGrossAmountBill / _stripeService.SmallestCurrencyUnit;
            var billingPlanTransferCost = _pricingeCalculationService.CalculateServiceProviderIncome
            (billingPlanPureAmount, contribution.PaymentInfo.CoachPaysStripeFee, paidTierOption.NormalizedFee, contribution.PaymentType, coachUser.CountryId).Total;
            var billingPlanInfo = new BillingPlanInfo
            {
                ProductBillingPlanId = productPlanResult.Payload,
                BillingPlanGrossCost = billingPlanGrossCost,
                BillingPlanPureCost = billingPlanPureAmount / _stripeService.SmallestCurrencyUnit,
                BillingPlanTransferCost = billingPlanTransferCost / _stripeService.SmallestCurrencyUnit,
                TotalBillingGrossCost = billingPlanGrossCost * paymentInfo.SplitNumbers.Value,
                TotalBillingPureCost = paymentInfo.Cost.Value
            };
            return OperationResult<BillingPlanInfo>.Success(null, billingPlanInfo);
        }

        private async Task<OperationResult<BillingPlanInfo>> AddContributionAssociatedMonthlySessionSubscriptionStripeProductPlan(
            ContributionBase contribution,
            PaidTierOption paidTierOption,
            string stripeStandardAccountId,
            string countryId)
        {
            MonthlySessionSubscription monthlySessionSubscriptionInfo = contribution.PaymentInfo.MonthlySessionSubscriptionInfo;

            if (contribution.PaymentInfo.MonthlySessionSubscriptionInfo == null)
            {
                return OperationResult<BillingPlanInfo>.Failure($"Contribution {nameof(monthlySessionSubscriptionInfo)} are not specified");
            }

            if (!contribution.PaymentInfo.MonthlySessionSubscriptionInfo.SessionCount.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure($"Contribution {nameof(monthlySessionSubscriptionInfo.SessionCount)} is not specified");
            }

            if (!contribution.PaymentInfo.MonthlySessionSubscriptionInfo.Duration.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure($"Contribution {nameof(monthlySessionSubscriptionInfo.Duration)} is not specified");
            }

            if (!contribution.PaymentInfo.MonthlySessionSubscriptionInfo.MonthlyPrice.HasValue)
            {
                return OperationResult<BillingPlanInfo>.Failure($"Contribution {nameof(monthlySessionSubscriptionInfo.MonthlyPrice)} is not specified");
            }

            PaymentSplitPeriods? splitPeriod = PaymentSplitPeriods.Monthly;
            int? duration = monthlySessionSubscriptionInfo.Duration;
            int? sessionCount = monthlySessionSubscriptionInfo.SessionCount;

            var productCreatingResult = await _stripeService.CreateProductAsync(
                new CreateProductViewModel { Id = contribution.Id, Name = contribution.Title, StandardAccountId = stripeStandardAccountId });

            if (productCreatingResult.Failed)
            {
                return OperationResult<BillingPlanInfo>.Failure(productCreatingResult.Message);
            }

            if (!StripePlanIntervals.TryGetValue(splitPeriod.Value, out var stripeInterval))
            {
                return OperationResult<BillingPlanInfo>.Failure(
                    $"Unsupported '{splitPeriod}' {nameof(splitPeriod)} type.");
            }

            var productId = productCreatingResult.Payload;

            var totalBillingPureCost = monthlySessionSubscriptionInfo.MonthlyPrice.Value * duration.Value;
            var billingPlanPureAmount = _pricingeCalculationService.CalculateBillingPlanCost(
                totalBillingPureCost * _stripeService.SmallestCurrencyUnit,
                duration.Value);
            var planGrossAmountBill = _paymentSystemFeeService.CalculateGrossAmountAsLong(
                billingPlanPureAmount, contribution.PaymentInfo.CoachPaysStripeFee, contribution.UserId);

            var createModel = new CreateProductPlanViewModel
            {
                ProductId = productId,
                Amount = planGrossAmountBill,
                Interval = stripeInterval,
                Duration = duration.Value,
                Name = splitPeriod.ToString()
            };

            var result = await _stripeService.CreateProductPlanAsync(createModel, contribution.DefaultCurrency, stripeStandardAccountId);

            if (result.Failed)
            {
                return OperationResult<BillingPlanInfo>.Failure(result.Message);
            }

            var billingPlanId = result.Payload;
            var billingPlanGrossCost = planGrossAmountBill / _stripeService.SmallestCurrencyUnit;

            var billingPlanTransferCost = _pricingeCalculationService.CalculateServiceProviderIncome(
                billingPlanPureAmount,
                contribution.PaymentInfo.CoachPaysStripeFee,
                paidTierOption.NormalizedFee,
                contribution.PaymentType, countryId).Total;

            var billingPlanInfo = new BillingPlanInfo
            {
                ProductBillingPlanId = billingPlanId,
                BillingPlanGrossCost = billingPlanGrossCost,
                BillingPlanPureCost = billingPlanPureAmount / _stripeService.SmallestCurrencyUnit,
                BillingPlanTransferCost = billingPlanTransferCost / _stripeService.SmallestCurrencyUnit,
                TotalBillingGrossCost = billingPlanGrossCost * duration.Value,
                TotalBillingPureCost = totalBillingPureCost
            };

            return OperationResult<BillingPlanInfo>.Success(null, billingPlanInfo);
        }

        public async Task<bool> IsContributionPurchasedByUser(string contributionId, string userId)
        {
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(p => p.ClientId == userId && p.ContributionId == contributionId);
            var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(x => x.Id == contributionId);
            var contributionAndStandardAccountIdDic = await _commonService.GetStripeStandardAccounIdFromContribution(contribution);
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);
            return purchaseVm != null && purchaseVm.HasSucceededPayment;
        }

        public async Task<OperationResult> AssignRoomIdAndNameToClass(ContributionBaseViewModel contributionVm,
            VideoRoomInfo videoRoomInfo, string classId)
        {
            OperationResult assignResult = contributionVm.AssignRoomInfoToClass(videoRoomInfo, classId);

            if (assignResult.Failed)
            {
                return OperationResult.Failure(assignResult.Message);
            }

            ContributionBase contribution = _mapper.Map<ContributionBase>(contributionVm);
            ContributionBase updatedContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            return OperationResult<ContributionBaseViewModel>.Success(_mapper.Map<ContributionBaseViewModel>(updatedContribution));
        }

        public async Task<CohealerInfoViewModel> GetCohealerInfoForClient(string cohealerUserId, string requestorAccountId)
        {
            var cohealerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == cohealerUserId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (cohealerUser is null)
            {
                return null;
            }

            var cohealerInfo = _mapper.Map<CohealerInfoViewModel>(cohealerUser);

            var cohealerContributions = await _contributionRootService.Get(c => c.UserId == cohealerUserId && c.Status == ContributionStatuses.Approved);
            var cohealerContributionsList = cohealerContributions.ToList();

            if (!cohealerContributionsList.Any())
            {
                return cohealerInfo;
            }

            var cohealerContributionsIds = cohealerContributionsList.Select(c => c.Id).ToList();

            var requestorPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p =>
                cohealerContributionsIds.Contains(p.ContributionId) && p.ClientId == requestorUser.Id);
            var purchasedContributionsIds = requestorPurchases.Select(p => p.ContributionId).ToList();

            var cohealerInfoContributions = _mapper.Map<IEnumerable<ContributionInCohealerInfoViewModel>>(cohealerContributionsList).ToList();
            foreach (var contributionInfo in cohealerInfoContributions)
            {
                contributionInfo.IsMeEnrolled = purchasedContributionsIds.Contains(contributionInfo.Id);
            }

            cohealerInfo.AvgContributionsRating = cohealerContributionsList.Average(c => c.Rating);
            cohealerInfo.ContributionCategories = cohealerContributionsList.SelectMany(c => c.Categories).Distinct().ToList();
            cohealerInfo.ContributionInfos = cohealerUser.Id == requestorUser.Id ? cohealerInfoContributions : cohealerInfoContributions.Where(e => e.IsMeEnrolled || !e.InvitationOnly).ToList();

            if (purchasedContributionsIds.Count > 0 && cohealerContributions.Count() > 0)
            {
                var filteredPurchasedContributions = cohealerContributionsList?.Where(m => purchasedContributionsIds.Contains(m.Id));
                if (filteredPurchasedContributions.Count() > 0)
                {
                    var coachesChatIds = filteredPurchasedContributions?.Select(m => m.Chat?.Sid).Distinct().ToList();
                    cohealerInfo.CoachChatIds = coachesChatIds.Count() > 0 ? coachesChatIds : null;
                }
            }
            return cohealerInfo;
        }

        public async Task<OperationResult> SetContributionClassAsCompleted(SetClassAsCompletedViewModel viewModel,
            string requestorAccountId)
        {
            var contribution = await _contributionRootService.GetOne(viewModel.ContributionId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (contribution is null)
            {
                return OperationResult.Failure($"Unable to find contribution with id {viewModel.ContributionId}");
            }

            if (!IsOwnerOrPartner(contribution, requestorUser))
            {
                return OperationResult.Failure("Forbidden to change contributions by other author");
            }

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            await FillPodsForSessionContribution(contributionVm);
            var result = contributionVm.SetClassAsCompleted(viewModel.ClassId);

            if (result.Failed)
            {
                return result;
            }


            var participantsIds = (List<string>)result.Payload;            
            contribution = _mapper.Map<ContributionBase>(contributionVm);
            if (contribution is SessionBasedContribution sessionCont)
            {
                foreach (var session in sessionCont.Sessions.Where(a => !a.IsPrerecorded))
                {
                    session.IsCompleted = !session.SessionTimes.Any(a => !a.IsCompleted);
                }
            }
            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            var isReleasingFromEscrowNeeded = false;
            switch (contribution.Type)
            {
                case nameof(ContributionMembership):
                case nameof(ContributionCommunity):
                case nameof(ContributionCourse):
                    {
                        var contributionCourse = contribution as SessionBasedContribution;
                        var firstSession = contributionCourse.Sessions.First();
                        isReleasingFromEscrowNeeded = firstSession.IsCompleted &&
                                                      firstSession.SessionTimes.Exists(st => st.Id == viewModel.ClassId);

                        participantsIds = isReleasingFromEscrowNeeded
                            ? contributionVm.GetBookedParticipantsIds()
                            : participantsIds;
                        break;
                    }
                case nameof(ContributionOneToOne):
                    {
                        isReleasingFromEscrowNeeded = true;
                        break;
                    }
                default:
                    throw new NotImplementedException();
            }

            if (isReleasingFromEscrowNeeded)
            {
                _jobScheduler.ScheduleJob<IMoveIncomeFromEscrowJob>(TimeSpan.FromSeconds(_escrowPeriodSeconds), viewModel.ContributionId, viewModel.ClassId,
                    participantsIds);

                _jobScheduler.ScheduleJob<IMoveRevenueFromEscrowJob>(TimeSpan.FromSeconds(_affiliateEscrowPeriodSeconds), viewModel.ContributionId, viewModel.ClassId,
                    participantsIds);
            }

            contributionVm.ConvertAllOwnUtcTimesToZoned(requestorUser.TimeZoneId);
            return OperationResult.Success(string.Empty, contributionVm);
        }

        public async Task<OperationResult> SetContributionSelfPacedClassAsCompleted(SetClassAsCompletedViewModel viewModel,
            string requestorAccountId)
        {
            var contribution = await _contributionRootService.GetOne(viewModel.ContributionId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (contribution is null)
            {
                return OperationResult.Failure($"Unable to find contribution with id {viewModel.ContributionId}");
            }

            var clientPurchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p =>
                p.ContributionId == viewModel.ContributionId && p.ClientId == requestorUser.Id);
            clientPurchases = clientPurchases.Where(c => c.Payments.Any(p =>
                p.PaymentStatus == Entity.Enums.Payments.PaymentStatus.Succeeded || p.PaymentStatus == Entity.Enums.Payments.PaymentStatus.Paid));
            if (!clientPurchases.Any())
            {
                return OperationResult.Failure("Participant is not associated with this contribution");
            }

            var contributionVm = _mapper.Map<SessionBasedContributionViewModel>(contribution);
            var podIds = contributionVm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
            contributionVm.Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();

            var result = contributionVm.SetSelfPacedClassAsCompleted(viewModel.ClassId, requestorUser.Id);

            if (result.Failed)
            {
                return result;
            }

            contribution = _mapper.Map<ContributionBase>(contributionVm);
            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            contributionVm.ConvertAllOwnUtcTimesToZoned(requestorUser.TimeZoneId);
            return OperationResult.Success(string.Empty, contributionVm);
        }

        private bool IsOwnerOrPartner(ContributionBase contribution, User requestorUser)
        {
            return requestorUser.Id == contribution.UserId || contribution.Partners.Any(x => x.IsAssigned && x.UserId == requestorUser.Id);
        }

        public async Task<OperationResult> SetContributionAsCompletedAsync(SetAsCompletedViewModel viewModel, string requestorAccountId)
        {
            var contribution = await _contributionRootService.GetOne(viewModel.ContributionId);
            var requestorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requestorAccountId);

            if (contribution == null)
            {
                return OperationResult.Failure($"Unable to find contribution with id {viewModel.ContributionId}");
            }

            if (!IsOwnerOrPartner(contribution, requestorUser))
            {
                return OperationResult.Failure("Forbidden to change contributions by other author");
            }

            if (contribution is ContributionOneToOne contributionOneToOne)
            {
                if (contributionOneToOne.GetAvailabilityTimes().Any(e => !e.Value.BookedTime.IsCompleted))
                {
                    return OperationResult.Failure("Unable to complete one-to-one contribution. Not all session completed.");
                }

                if (contributionOneToOne.PackagePurchases.Any(e => !e.IsCompleted))
                {
                    return OperationResult.Failure("Unable to complete one-to-one contribution. Not all packages completed.");
                }
            }

            if (contribution is SessionBasedContribution contributionCourse)
            {
                if (contributionCourse.Sessions.Any(e => !e.IsCompleted && !e.IsPrerecorded))
                {
                    return OperationResult.Failure("Unable to complete contribution. Not all sessions completed.");
                }
            }

            //if (contribution.Status != ContributionStatuses.Approved)
            //{
            //    return OperationResult.Failure("Unable to complete contribution. Only approved contribution is enabled for completion");
            //}

            contribution.Status = ContributionStatuses.Completed;
            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            contributionVm.ConvertAllOwnUtcTimesToZoned(requestorUser.TimeZoneId);
            return OperationResult.Success(string.Empty, contributionVm);
        }

        public async Task<OperationResult> AddAttachmentToContribution(ContributionBase contribution, string sessionId, Document document)
        {
            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            if (contributionVm is SessionBasedContributionViewModel sessionBased)
            {
                var liveSessions = sessionBased.Sessions?.FirstOrDefault(m => m.Id == sessionId);
                if (liveSessions != null)
                {
                    AddAttachementInSessions(sessionId, document, contributionVm);
                }
                else
                {
                    var selfPacesSessionOnly = sessionBased?.Sessions.Where(m => m.IsPrerecorded == true).SelectMany(m => m?.SessionTimes);
                    if (selfPacesSessionOnly.Count() > 0)
                    {
                        var requiredSessionTimeArray = selfPacesSessionOnly.FirstOrDefault(m => m.Id == sessionId);
                        if (requiredSessionTimeArray is null)
                        {
                            return OperationResult.Failure($"Unable to find Sessions with Id {sessionId}");
                        }
                        requiredSessionTimeArray.Attachments.Add((Document)document);
                    }
                    else
                    {
                        return OperationResult.Failure($"Unable to find attachments.");
                    }
                }

            }
            else
            {
                AddAttachementInSessions(sessionId, document, contributionVm);
            }
            var contributionWithAttachment = _mapper.Map<ContributionBase>(contributionVm);
            var contributionUpdated = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contributionWithAttachment);

            return OperationResult.Success(string.Empty, _mapper.Map<ContributionBaseViewModel>(contributionUpdated));
        }
        private OperationResult AddAttachementInSessions(string sessionId, Document document, ContributionBaseViewModel contributionVm)
        {
            var addAttachmentResult = contributionVm.AddAttachment(sessionId, document);
            if (!addAttachmentResult.Succeeded)
            {
                return addAttachmentResult;
            }
            return OperationResult.Success();

        }
        public async Task<OperationResult> RemoveAttachmentFromContribution(ContributionBase contribution, string sessionId, string documentId)
        {
            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            if (contributionVm is SessionBasedContributionViewModel sessionBased)
            {
                var liveSessions = sessionBased.Sessions?.FirstOrDefault(m => m.Id == sessionId);
                if (liveSessions != null)
                {
                    var removeAttachmentResult = contributionVm.RemoveAttachment(sessionId, documentId);
                    if (!removeAttachmentResult.Succeeded)
                    {
                        return removeAttachmentResult;
                    }
                }
                else
                {
                    var selfPacesSessionOnly = sessionBased?.Sessions.Where(m => m.IsPrerecorded == true).SelectMany(m => m?.SessionTimes);
                    if (selfPacesSessionOnly?.Count() > 0)
                    {
                        var requiredSessionTimeArray = selfPacesSessionOnly.FirstOrDefault(m => m.Id == sessionId);
                        if (requiredSessionTimeArray is null)
                        {
                            return OperationResult.Failure($"Unable to find Sessions with Id {sessionId}");
                        }
                        var sessionTimeAttachments = requiredSessionTimeArray.Attachments;
                        if (sessionTimeAttachments?.Count() == 0)
                        {
                            return OperationResult.Failure($"Unable to find Attachments in class with Id {sessionId}");
                        }
                        var documentToRemove = requiredSessionTimeArray?.Attachments.FirstOrDefault(m => m.Id == documentId);
                        if (documentToRemove != null)
                        {
                            sessionTimeAttachments.Remove((Document)documentToRemove);
                        }
                    }
                    else
                    {
                        return OperationResult.Failure($"Unable to find class with Id: {sessionId}");
                    }

                }
            }
            var contributionWithAttachment = _mapper.Map<ContributionBase>(contributionVm);
            var contributionUpdated =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contributionWithAttachment);

            return OperationResult.Success(string.Empty, _mapper.Map<ContributionBaseViewModel>(contributionUpdated));
        }

        public OperationResult GetAttachmentFromContribution(ContributionBase contribution, string sessionId, string documentId)
        {
            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            if (contributionVm is SessionBasedContributionViewModel sessionBased)
            {
                var liveSessions = sessionBased.Sessions?.FirstOrDefault(m => m.Id == sessionId);
                if (liveSessions != null)
                {
                    return contributionVm.GetAttachment(documentId);
                }
                else
                {
                    var selfPacesSessionOnly = sessionBased.Sessions?.Where(m => m.IsPrerecorded == true).SelectMany(m => m?.SessionTimes);
                    if (selfPacesSessionOnly.Count() > 0)
                    {
                        var requiredSessionTimeArray = selfPacesSessionOnly.FirstOrDefault(m => m.Id == sessionId);
                        if (requiredSessionTimeArray is null)
                        {
                            return OperationResult.Failure($"Unable to find Sessions with Id {sessionId}");
                        }
                        var sessionTimeAttachments = requiredSessionTimeArray.Attachments;
                        if (sessionTimeAttachments.Count() == 0)
                        {
                            return OperationResult.Failure($"Unable to find Attachments in class with Id {sessionId}");
                        }
                        var document = requiredSessionTimeArray?.Attachments?.FirstOrDefault(m => m.Id == documentId);
                        if (document == null)
                        {
                            return OperationResult.Failure($"Unable to find document.");
                        }
                        return OperationResult.Success(string.Empty, document);
                    }
                    else
                    {
                        return OperationResult.Failure($"Unable to find class with Id: {sessionId}");
                    }
                }
            }
            else
            {
                return contributionVm.GetAttachment(documentId);
            }
        }
        public async Task<OperationResult<ContributionBaseViewModel>> AssignPartnerCoachToContribution(string contributionId, string contributionOwnerUserId, string assignCode)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution == null)
                return new OperationResult<ContributionBaseViewModel>(false, "Contribution not found");

            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);

            if (contribution.UserId != contributionOwnerUserId)
                return new OperationResult<ContributionBaseViewModel>(false, "Invalid contribution to assign", contributionVm);

            if (contribution.Partners?.Any(x => x.IsAssigned && x.AssignCode == assignCode) == true)
                return new OperationResult<ContributionBaseViewModel>(true, "Partner coach has already been added", contributionVm);

            var partnerToAssign = contribution.Partners?.FirstOrDefault(x => x.AssignCode == assignCode);

            if (partnerToAssign == null)
                return new OperationResult<ContributionBaseViewModel>(false, "Invalid assign code", contributionVm);

            var partnerAccount = (await _unitOfWork.GetRepositoryAsync<Account>().Get(x => x.Email == partnerToAssign.PartnerEmail))?.FirstOrDefault();
            if (partnerAccount == null)
                return new OperationResult<ContributionBaseViewModel>(false, "You must be registered with the email to which you received the invitation letter", contributionVm);

            var partnerUser = (await _unitOfWork.GetRepositoryAsync<User>().Get(x => x.AccountId == partnerAccount.Id))?.FirstOrDefault();
            if (partnerUser == null)
                return new OperationResult<ContributionBaseViewModel>(false, "You must be registered with the email to which you received the invitation letter", contributionVm);

            if (!partnerAccount.Roles.Contains(Roles.Cohealer))
                return new OperationResult<ContributionBaseViewModel>(false, "You must be registered as Coach", contributionVm);


            partnerToAssign.IsAssigned = true;
            partnerToAssign.UserId = partnerUser.Id;

            var existingChatsUserIds = contribution.Chat.CohealerPeerChatSids.Select(x => x.Key);
            List<PartnerPeerChat> partnerChats = new List<PartnerPeerChat>();
            foreach (var clientUserId in existingChatsUserIds)
            {
                var peerChatResult = await _chatService.CreatePeerChat(partnerAccount.Id, clientUserId);
                var peerChat = peerChatResult.Payload as PeerChat;
                partnerChats.Add(new PartnerPeerChat
                {
                    UserId = clientUserId,
                    ChatSid = peerChat.Sid
                });
            }

            contribution.Chat.PartnerChats.Add(new PartnerChats
            {
                PartnerUserId = partnerUser.Id,
                PeerChats = partnerChats
            });

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
            if (contribution.Chat != null)
                await _chatService.AddUserToChat(partnerUser.Id, contribution.Chat.Sid);

            try
            {
                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.Id == partnerToAssign.UserId);
                if (user != null)
                {
                    user.IsPartnerCoach = true;
                    await _unitOfWork.GetRepositoryAsync<User>().Update(user.Id, user);
                }
            }
            catch { }

            try
            {
                var contributionCourse = contribution as SessionBasedContribution;

                var locationUrl = contributionCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(contributionCourse.Id));

                await _notifictionService.SendLiveCourseWasUpdatedNotificationAsync(
                    contribution.Title,
                    new Dictionary<string, bool> { { partnerToAssign.UserId, false } },
                    locationUrl,
                    contributionCourse.GetSessionTimes($"{partnerUser.FirstName} {partnerUser.LastName}", withPreRecorded: false).Values.ToList(), contribution.UserId, contribution.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error while notifying partner coach");
            }

            return new OperationResult<ContributionBaseViewModel>(true, "Partner successfully added to contribution", contributionVm);
        }

        public async Task<OperationResult<string>> CreatePartnerCoachAssignRequest(string contributionId, string contributionOwnerId, string partnerEmail)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution == null)
                return new OperationResult<string>(false, "Contribution not found");

            if (contribution.UserId != contributionOwnerId)
                return new OperationResult<string>(false, "You must be owner to assign partners");

            if (contribution.Type == nameof(ContributionOneToOne))
                return new OperationResult<string>(false, "Contribution must be Live Course Type or Membership or Community");

            ContributionPartner existingPartner = contribution.Partners.FirstOrDefault(p => p.PartnerEmail?.ToLower()?.Trim() == partnerEmail?.ToLower()?.Trim());
            if (existingPartner != null)
            {
                // resend the email
                if (!existingPartner.IsAssigned)
                {
                    return new OperationResult<string>(existingPartner.AssignCode);
                }
                else
                {
                    return new OperationResult<string>(false, "Partner coach has already been added");
                }
            }

            var random = new Random();
            var assignCode = random.Next(1, Int32.MaxValue - 1).ToString();
            contribution.Partners = contribution.Partners ?? new List<ContributionPartner>();

            contribution.Partners.Add(new ContributionPartner { IsAssigned = false, AssignCode = assignCode, PartnerEmail = partnerEmail.ToLower() });

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            return new OperationResult<string>(assignCode);

        }
        public async Task<OperationResult<GroupedTableContributionViewModel>> GetPartnerContributions(string accountId, string type = null, List<ContributionStatuses> contributionStatuses = null, bool fromDashboard = false)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == accountId);
            var clientTimeZone = await _unitOfWork.GetRepositoryAsync<Entity.Entities.TimeZone>().GetOne(m => m.CountryName == user.TimeZoneId);

            if (user == null)
            {
                return new OperationResult<GroupedTableContributionViewModel>(false, "User not found");
            }

            var availablePartnerContributionTypes = string.IsNullOrEmpty(type)
                ? new List<string>() { nameof(ContributionCourse), nameof(ContributionMembership), nameof(ContributionCommunity) }
                : new List<string>() { type };

            var contributions = (await _contributionRootService.Get(x => x.Partners.Any(y => y.IsAssigned && y.UserId == user.Id)))
                .Where(x => availablePartnerContributionTypes.Contains(x.Type) && !x.Status.Equals(ContributionStatuses.Completed));

            if (contributionStatuses != null && contributionStatuses.Any())
            {
                contributions = contributions.Where(x => contributionStatuses.Contains(x.Status));
            }

            var contributionBaseVms = _mapper.Map<IList<ContributionBaseViewModel>>(contributions);
            contributionBaseVms?.ToList().ForEach(m=>m?.ConvertAllOwnUtcTimesToZoned(user?.TimeZoneId));

            foreach (var contribution in contributionBaseVms)
            {
                await FillPodsForSessionContribution(contribution);
            }
            var sessions = contributionBaseVms.Select(x => new { Contribution = x, Sessions = x.GetClosestCohealerSessions(fromDashboard) });
            sessions = sessions.Where(s => s.Sessions.Count > 0).ToList();
            var upcomingSessions = new List<ClosestCohealerSession>();

            var contribTableVms = new List<ContribTableViewModel>();

            foreach (var pair in sessions)
            {
                var closestSession = pair.Sessions.Where(m => !m.IsPrerecorded)?.FirstOrDefault();
                var currentClosestSession = new ClosestCohealerSession();
                if (closestSession != null)
                {
                    currentClosestSession = new ClosestCohealerSession
                    {
                        ContributionName = pair.Contribution.Title,
                        ContributionId = pair.Contribution.Id,
                        Title = closestSession?.Name == "Session" ? closestSession?.Title : closestSession?.Name,
                        StartTime = closestSession.StartTime,
                        EnrolledTotal = closestSession is null ? 0 : closestSession.EnrolledTotal,
                        EnrolledMax = closestSession is null ? 0 : closestSession.EnrolledMax,
                        ClassId = closestSession?.ClassId,
                        ClassGroupId = closestSession?.ClassGroupId,
                        TimezoneId = user.TimeZoneId,
                        Type = pair.Contribution?.Type,
                        ChatSid = closestSession?.ChatSid,
                        LiveVideoServiceProvider = pair.Contribution.LiveVideoServiceProvider,
                        IsPrerecorded = closestSession?.IsPrerecorded,
                        SessionTimes = closestSession?.SessionTimes,
                        IsCompleted = closestSession.IsCompleted,
                        TimeZone = user?.TimeZoneId,
                        GroupSessions = pair.Contribution is SessionBasedContributionViewModel sessionBasedCloset ? sessionBasedCloset?.Sessions?.Where(m => m.Id == closestSession?.ClassGroupId).FirstOrDefault() : null,
                        OneToOneSessions = pair.Contribution is ContributionOneToOneViewModel oneToOneBased ? oneToOneBased?.AvailabilityTimes : null,

                    };
                    foreach (var session in pair.Sessions.Where(m => !m.IsCompleted))
                    {
                        upcomingSessions.Add(PopulatePartnerSessionData(session, pair.Contribution, user.TimeZoneId));
                    }
                    await FillPodsForSessionContribution(pair.Contribution);
                    contribTableVms.Add(await PopulatePartnerContributionData(pair.Contribution, currentClosestSession, closestSession, clientTimeZone?.ShortName));
                }
                else if (!fromDashboard)
                {
                    foreach (var session in pair.Sessions.Where(m => !m.IsCompleted))
                    {
                        upcomingSessions.Add(PopulatePartnerSessionData(session, pair.Contribution, user.TimeZoneId));
                    }
                    await FillPodsForSessionContribution(pair.Contribution);
                    contribTableVms.Add(await PopulatePartnerContributionData(pair.Contribution, currentClosestSession, closestSession, clientTimeZone?.ShortName));
                }

            }
            var closesClassForBanner = await GetClosestClassForBanner(contributionBaseVms);

            var result = new GroupedTableContributionViewModel
            {
                Contributions = contribTableVms?.OrderByDescending(m => m.CreateTime),
                Type = contribTableVms.FirstOrDefault()?.Type,
                UpcomingSessions = upcomingSessions,
                ContributionImage = contribTableVms.FirstOrDefault()?.PreviewContentUrls?.FirstOrDefault(),
                ClosestClassForBanner = closesClassForBanner
            };

            return new OperationResult<GroupedTableContributionViewModel>(result);
        }
        private async Task<ContribTableViewModel> PopulatePartnerContributionData(ContributionBaseViewModel contribution, ClosestCohealerSession currentClosestSession, ClosestCohealerSessionInfo closestSession, string timeZoneShortName)
        {
            
            var contribTableVm = _mapper.Map<ContribTableViewModel>(contribution);
            contribTableVm.EarnedRevenue = await _cohealerIncomeService.GetContributionRevenueAsync(contribTableVm.Id);
            contribTableVm.Currency = contribution.DefaultCurrency?.ToUpper() ?? "USD"; //By Uzair
            contribTableVm.Symbol = contribution.DefaultSymbol ?? "$";
            contribTableVm.ClosestSession = closestSession != null ? currentClosestSession : null;
            contribTableVm.ContributionImage = contribution.PreviewContentUrls?.FirstOrDefault();
            contribTableVm.IsInvoiced = contribution.IsInvoiced;
            contribTableVm.CreateTime = contribution.CreateTime;
            contribTableVm.TimeZoneShortForm = timeZoneShortName;

            return contribTableVm; ;
        }
        private ClosestCohealerSession PopulatePartnerSessionData(ClosestCohealerSessionInfo session, ContributionBaseViewModel contribution, string timeZone)
        {
            var model = new ClosestCohealerSession
            {
                ContributionName = contribution.Title,
                ContributionId = contribution.Id,
                Title = session?.Name == "Session" ? session.Title : session.Name,
                StartTime = session.StartTime,
                EnrolledTotal = session.EnrolledTotal,
                EnrolledMax = session.EnrolledMax,
                ClassId = session.ClassId,
                ClassGroupId = session.ClassGroupId,
                TimezoneId = timeZone,
                Type = contribution.Type,
                ChatSid = session.ChatSid,
                LiveVideoServiceProvider = contribution.LiveVideoServiceProvider,
                IsPrerecorded = session?.IsPrerecorded
            };

            return model;
        }

        public async Task<OperationResult> DeletePartnerFromContribution(string contributionId, string partnerUserId, string requsetorAccountId)
        {
            var accountRepository = _unitOfWork.GetRepositoryAsync<Account>();
            var userRepository = _unitOfWork.GetRepositoryAsync<User>();

            var requesterUser = await userRepository.GetOne(x => x.AccountId == requsetorAccountId);
            var account = await accountRepository.GetOne(x => x.Id == requsetorAccountId);
            var isAdmin = account.Roles.Any(x => x == Roles.Admin || x == Roles.SuperAdmin);

            var contribution = await _contributionRootService.GetOne(x => x.Id == contributionId);
            if (!isAdmin && contribution.UserId != requesterUser.Id)
            {
                return new OperationResult(false, "You have no access to manage this contribution");
            }

            var partnerToDelete = contribution.Partners.FirstOrDefault(x => x.UserId == partnerUserId);
            if (partnerToDelete == null)
            {
                return new OperationResult(false, "Partner not found");
            }

            contribution.Partners.Remove(partnerToDelete);

            //disable chats related to Contribution
            var allPartnerCoachContributions = await _contributionRootService.Get(x => x.UserId == partnerUserId);
            var allOwnedChats = allPartnerCoachContributions
                .Where(x => x.Chat != null)
                .SelectMany(x => x.Chat?.CohealerPeerChatSids?.Select(y => y.Value)).Distinct().ToList();

            var partnerChats = contribution.Chat?.PartnerChats?.FirstOrDefault(x => x.PartnerUserId == partnerUserId);
            if (partnerChats != null)
                contribution.Chat.PartnerChats.Remove(partnerChats);

            foreach (var contributionPartnerChat in partnerChats?.PeerChats)
            {
                User partnerUser = null;
                if (!allOwnedChats.Contains(contributionPartnerChat.ChatSid))
                {
                    if (partnerUser == null)
                        partnerUser = await userRepository.GetOne(x => x.Id == partnerUserId);
                    await _chatService.LeavePeerChat(contributionPartnerChat.ChatSid, partnerUser.AccountId);
                }
            }
            await _chatService.RemoveUserFromChat(partnerUserId, contribution.Chat.Sid);
            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);

            return new OperationResult(true, "Partner removed successfully");
        }

        public async Task<OperationResult<List<ContributionPartnerViewModel>>> GetContributionPartnersAsync(string contributionId)
        {
            var partnerList = new List<ContributionPartnerViewModel>();
            var userRepository = _unitOfWork.GetRepositoryAsync<User>();
            var contribution = await _contributionRootService.GetOne(contributionId);

            if (contribution is null)
            {
                return OperationResult<List<ContributionPartnerViewModel>>.Failure("Contribution not fount");
            }

            foreach (var partner in contribution.Partners)
            {
                var partnerUser = await userRepository.GetOne(p => p.Id == partner.UserId);
                if (partnerUser != null)
                {
                    partnerList.Add(new ContributionPartnerViewModel
                    {
                        UserId = partnerUser.Id,
                        AvatarUrl = partnerUser.AvatarUrl,
                        FirstName = partnerUser.FirstName,
                        LastName = partnerUser.LastName
                    });
                }
            }
            return new OperationResult<List<ContributionPartnerViewModel>>(partnerList);
        }

        public async Task<IEnumerable<(string ContributionId, DeclinedSubscriptionPurchase DeclinedSubscriptionPurchase)>> GetClientContributionIdsWithDeclinedSubscription(string accountId)
        {
            var client = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ClientId == client.Id && p.DeclinedSubscriptionPurchase != null);
            var purchaseVms = _mapper.Map<IEnumerable<PurchaseViewModel>>(purchases).ToList();
            var contributionAndStandardAccountIdDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic));
            return purchaseVms.Where(p => p.HasSucceededPayment).Select(c => (c.ContributionId, c.DeclinedSubscriptionPurchase));
        }

        public async Task<OperationResult<ContributionOneToOneViewModel>> RescheduleOneToOneCoachBooking(
            string coachAccountId,
            string contributionId,
            string rescheduleFromId,
            string rescheduleToId,
            string reschedulingNotes,
            int sessionOffsetInMinutes)
        {
            var userRepository = _unitOfWork.GetRepositoryAsync<User>();

            var cohealerUser = await userRepository.GetOne(x => x.AccountId == coachAccountId);

            var contribution = await _contributionRootService.GetOne(contributionId);

            if (!(contribution is ContributionOneToOne contributionOneToOne))
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("only one-to-one contributions allowed");
            }

            var validationResult = ValidateContribution(contributionOneToOne);
            if (validationResult.Failed)
            {
                return validationResult;
            }

            var reschedulingResult = await RescheduleOneToOne(
                cohealerUser.Id,
                rescheduleFromId,
                rescheduleToId,
                reschedulingNotes,
                sessionOffsetInMinutes,
                contributionOneToOne,
                CoachReschedulingPermissionValidation);
            if (reschedulingResult.Failed)
            {
                return reschedulingResult;
            }

            var updatedContribution = await GetCohealerContributionByIdAsync(contribution.Id, coachAccountId);

            return OperationResult<ContributionOneToOneViewModel>.Success(string.Empty, updatedContribution.Payload as ContributionOneToOneViewModel);
        }

        private async Task<OperationResult<ContributionOneToOneViewModel>> RescheduleOneToOne(
            string coachOrClientUserId,
            string rescheduleFromId,
            string rescheduleToId,
            string reschedulingNotes,
            int sessionOffsetInMinutes,
            ContributionOneToOne contributionOneToOne,
            HasReschedulingPermission hasReschedulingPermission)
        {
            var availabilityTimesToBookedTimes = contributionOneToOne.GetAvailabilityTimes();

            if (!availabilityTimesToBookedTimes.TryGetValue(rescheduleFromId, out var rescheduleFrom))
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("From booked time not found");
            }

            var permissionCheckResult = hasReschedulingPermission(contributionOneToOne, coachOrClientUserId, rescheduleFromId);
            if (permissionCheckResult.Failed)
            {
                return permissionCheckResult;
            }

            var sourceSlotValidationResult = ValidateSourceSlot(rescheduleFrom);

            if (sourceSlotValidationResult.Failed)
            {
                return sourceSlotValidationResult;
            }


            var scheduledSlots = await _contributionRootService.GetAvailabilityTimesForCoach(contributionOneToOne.Id, sessionOffsetInMinutes, timesInUtc: true);

            var rescheduleTo = scheduledSlots.FirstOrDefault(e => e.Id == rescheduleToId);

            var destinationSlotValidationResult = ValidateDestinationSlot(rescheduleTo);
            if (destinationSlotValidationResult.Failed)
            {
                return destinationSlotValidationResult;
            }

            SwapSlots(rescheduleFrom, rescheduleTo);

            if (!contributionOneToOne.AvailabilityTimes.Contains(rescheduleTo))
            {
                contributionOneToOne.AvailabilityTimes.Add(rescheduleTo);
            }

            if (!rescheduleFrom.AvailabilityTime.BookedTimes.Any())
            {
                contributionOneToOne.AvailabilityTimes.Remove(rescheduleFrom.AvailabilityTime);
            }

            await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionOneToOne.Id, contributionOneToOne);

            await NotifyCoachAndClientAboutRescheduling(reschedulingNotes, contributionOneToOne, rescheduleFrom);

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        private void SwapSlots(BookedTimeToAvailabilityTime rescheduleFrom, AvailabilityTime rescheduleTo)
        {
            rescheduleFrom.AvailabilityTime.BookedTimes.Remove(rescheduleFrom.BookedTime);
            rescheduleTo.BookedTimes.Add(rescheduleFrom.BookedTime);

            rescheduleFrom.BookedTime.StartTime = rescheduleTo.StartTime;
            rescheduleFrom.BookedTime.EndTime = rescheduleTo.EndTime;
        }

        private async Task NotifyCoachAndClientAboutRescheduling(string reschedulingNotes, ContributionOneToOne contributionOneToOne, BookedTimeToAvailabilityTime rescheduleFrom)
        {
            var clientUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == rescheduleFrom.BookedTime.ParticipantId);
            bool sendIcalAttachment = true;

            try
            {
                var locationUrl = contributionOneToOne.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(contributionOneToOne.Id));

                //Send Nylas Event
                try
                {
                    var updated = _mapper.Map<ContributionBase>(contributionOneToOne);

                    //Nylas Event creation if External calendar is attached with contribution
                    var coach = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == contributionOneToOne.UserId);
                    if (!string.IsNullOrWhiteSpace(contributionOneToOne.ExternalCalendarEmail))
                    {
                        var NylasAccount = await _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == coach.AccountId && n.EmailAddress.ToLower() == contributionOneToOne.ExternalCalendarEmail.ToLower());
                        if (NylasAccount != null && !string.IsNullOrEmpty(contributionOneToOne.ExternalCalendarEmail))
                        {
                            if (!string.IsNullOrEmpty(NylasAccount.CalendarId))
                            {
                                if (rescheduleFrom.BookedTime.EventInfo.CalendarId == NylasAccount.CalendarId)
                                {
                                    CalendarEvent calevent = _mapper.Map<CalendarEvent>(rescheduleFrom);
                                    calevent.Location = locationUrl;
                                    calevent.Description = contributionOneToOne.CustomInvitationBody;
                                    NylasEventCreation eventResponse = await _notifictionService.CreateorUpdateCalendarEvent(calevent, clientUser.Id, NylasAccount, rescheduleFrom, true, rescheduleFrom.BookedTime.EventInfo.CalendarEventID);
                                    //rescheduleFrom.BookedTime.CalendarEventID = eventResponse.id;
                                    //rescheduleFrom.BookedTime.CalendarId = eventResponse.calendar_id;
                                    EventInfo eventInfo = new EventInfo()
                                    {
                                        CalendarEventID = eventResponse.id,
                                        CalendarId = eventResponse.calendar_id,
                                        NylasAccountId = eventResponse.account_id,
                                        AccessToken = NylasAccount.AccessToken,
                                        ParticipantId = clientUser.Id
                                    };
                                    rescheduleFrom.BookedTime.EventInfo = eventInfo;

                                    sendIcalAttachment = false;
                                    var ucourse = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(updated.Id, updated);
                                }

                            }
                        }
                    }
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during sending rescheduling Nylas Invite to client/coach");

                }
                await _notifictionService.SendOneToOneReschedulingNotificationToClient(contributionOneToOne.Id,
                    contributionOneToOne.Title,
                    contributionOneToOne.UserId,
                    reschedulingNotes,
                    locationUrl,
                    new List<BookedTimeToAvailabilityTime> { rescheduleFrom }, contributionOneToOne.CustomInvitationBody, sendIcalAttachment);



            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sending rescheduling email to client");
            }

            try
            {
                var locationUrl = contributionOneToOne.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(contributionOneToOne.Id));
                rescheduleFrom.ClientName = $"{clientUser.FirstName} {clientUser.LastName}";
                await _notifictionService.SendOneToOneReschedulingNotificationToCoach(contributionOneToOne.Id,
                    contributionOneToOne.Title,
                    clientUser.Id,
                    reschedulingNotes,
                    locationUrl,
                    new List<BookedTimeToAvailabilityTime>() { rescheduleFrom }, contributionOneToOne.CustomInvitationBody, sendIcalAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sending rescheduling email to coach");
            }
        }

        private OperationResult<ContributionOneToOneViewModel> ValidateDestinationSlot(AvailabilityTime rescheduleTo)
        {
            if (rescheduleTo == null)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("To booked time not found");
            }

            if (rescheduleTo.BookedTimes.Count > 0)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("To booked time already booked");
            }

            if (rescheduleTo.StartTime <= DateTime.UtcNow)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("Rescheduling not allowed to past session");
            }

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        private OperationResult<ContributionOneToOneViewModel> ValidateSourceSlot(BookedTimeToAvailabilityTime rescheduleFrom)
        {
            if (rescheduleFrom.BookedTime.ParticipantId == null)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("From booked time has not participant");
            }

            if (rescheduleFrom.BookedTime.IsCompleted)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("Session is completed");
            }

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        private OperationResult<ContributionOneToOneViewModel> ValidateContribution(ContributionBase contribution)
        {
            if (contribution == null)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("contribution not found");
            }

            if (!(contribution is ContributionOneToOne))
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("only 1-1 contributions allowed");
            }

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        public async Task<OperationResult<ContributionOneToOneViewModel>> RescheduleOneToOneClientBooking(
            string clientAccountId,
            string contributionId,
            string rescheduleFromId,
            string rescheduleToId,
            string reschedulingNotes,
            int sessionOffsetInMinutes)
        {
            var userRepository = _unitOfWork.GetRepositoryAsync<User>();

            var clientUser = await userRepository.GetOne(x => x.AccountId == clientAccountId);

            var contribution = await _contributionRootService.GetOne(contributionId);

            if (!(contribution is ContributionOneToOne contributionOneToOne))
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("only one-to-one contribution supported");
            }

            var validationResult = ValidateContribution(contributionOneToOne);
            if (validationResult.Failed)
            {
                return validationResult;
            }

            var reschedulingResult = await RescheduleOneToOne(
                clientUser.Id,
                rescheduleFromId,
                rescheduleToId,
                reschedulingNotes,
                sessionOffsetInMinutes,
                contributionOneToOne,
                ClientReschedulingPermissionValidation);
            if (reschedulingResult.Failed)
            {
                return reschedulingResult;
            }

            var updatedContribution = await GetClientContributionByIdAsync(contribution.Id, clientAccountId);

            return OperationResult<ContributionOneToOneViewModel>.Success(string.Empty, updatedContribution as ContributionOneToOneViewModel);
        }


        private delegate OperationResult<ContributionOneToOneViewModel> HasReschedulingPermission(ContributionOneToOne contributionOneToOne, string userId, string rescheduleFromId);

        private readonly TimeSpan ClientReschedulingAllowedPeriod = TimeSpan.FromHours(24);

        public OperationResult<ContributionOneToOneViewModel> ClientReschedulingPermissionValidation(ContributionOneToOne contributionOneToOne, string clientUserId, string rescheduleFromId)
        {
            var sourceSlot = contributionOneToOne.GetBookedTimeById(rescheduleFromId);
            if (sourceSlot.ParticipantId != clientUserId)
            {
                return OperationResult<ContributionOneToOneViewModel>.Forbid("not allowed");
            }

            if (DateTime.UtcNow + ClientReschedulingAllowedPeriod >= sourceSlot.StartTime)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure($"You can reschedule only {ClientReschedulingAllowedPeriod.TotalHours.ToString()} hours before session starts");
            }

            if (sourceSlot.IsCompleted)
            {
                return OperationResult<ContributionOneToOneViewModel>.Failure("Completed session not allowed to rescheduled");
            }

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        public OperationResult<ContributionOneToOneViewModel> CoachReschedulingPermissionValidation(ContributionOneToOne contributionOneToOne, string coachUserId, string rescheduleFromId)
        {
            if (contributionOneToOne.UserId != coachUserId)
            {
                return OperationResult<ContributionOneToOneViewModel>.Forbid("not allowed");
            }

            return OperationResult<ContributionOneToOneViewModel>.Success();
        }

        public async Task<OperationResult<IEnumerable<string>>> GetCohealerContributionIds(string cohealerAccountId)
        {
            try
            {
                var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == cohealerAccountId);
                var cohealerContributions = await _contributionRootService.Get(e => e.UserId == user.Id);

                var cohealerContributionIds = cohealerContributions.Select(e => e.Id);

                return OperationResult<IEnumerable<string>>.Success(string.Empty, cohealerContributionIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during listing cohealer contribution ids");
            }

            return OperationResult<IEnumerable<string>>.Failure("error during listing cohealer contribution ids");
        }

        public async Task<OperationResult<IEnumerable<string>>> GetPartnerContributionIds(string partnerAccountId)
        {
            try
            {
                var partnerUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == partnerAccountId);
                var partneredContribution = await _contributionRootService.Get(e => e.Partners.Any(j => j.IsAssigned && j.UserId == partnerUser.Id));

                var partneredContributionIds = partneredContribution.Select(e => e.Id);

                return OperationResult<IEnumerable<string>>.Success(string.Empty, partneredContributionIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during listing partner contribution ids");
            }

            return OperationResult<IEnumerable<string>>.Failure("error during listing partner contribution ids");
        }

        public async Task<IEnumerable<FailedSubscription>> ListClientIncompleteSubscription(string clientAccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == clientAccountId);

            var declinedContributions = await GetClientContributionIdsWithDeclinedSubscription(clientAccountId);

            var clientContributionIds = declinedContributions.Select(e => e.ContributionId);

            var clientContributions = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(e => clientContributionIds.Contains(e.Id));

            var contributions = clientContributions.ToDictionary(e => e.Id);

            return declinedContributions.Select(e => new FailedSubscription
            {
                ContributionName = contributions[e.ContributionId].Title,
                ClientName = $"{user.FirstName} {user.LastName}",
                DeclinedSubscriptionPurchase = e.DeclinedSubscriptionPurchase
            });
        }

        public async Task<IEnumerable<FailedSubscription>> ListCoachIncompleteSubscriptions(string coachAccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(x => x.AccountId == coachAccountId);

            var allCreatedByCohealer = await GetCohealerContributionIds(coachAccountId);
            if (allCreatedByCohealer.Failed)
            {
                throw new Exception(allCreatedByCohealer.Message);
            }

            var allPartnered = await GetPartnerContributionIds(coachAccountId);
            if (allPartnered.Failed)
            {
                throw new Exception(allPartnered.Message);
            }

            var allContributionIds = allCreatedByCohealer.Payload.Concat(allPartnered.Payload).ToHashSet();

            var allContributionsWithSubscription = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(e =>
                allContributionIds.Contains(e.Id) &&
                e.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments));


            var declinedSubscriptions = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(e =>
               e.IsFirstPaymentHandeled &&
               allContributionIds.Contains(e.ContributionId) &&
               !string.IsNullOrEmpty(e.SubscriptionId) &&
               e.DeclinedSubscriptionPurchase != null);


            var declinedContributionIds = declinedSubscriptions.Select(e => e.ContributionId);
            var declinedContributionsDict = (await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(e => declinedContributionIds.Contains(e.Id))).ToDictionary(e => e.Id);

            var declinedUserIds = declinedSubscriptions.Select(e => e.ClientId);
            var declinedUsersDict = (await _unitOfWork.GetRepositoryAsync<User>().Get(e => declinedUserIds.Contains(e.Id))).ToDictionary(e => e.Id);

            return declinedSubscriptions.Select(e =>
            {
                var client = declinedUsersDict[e.ClientId];
                return new FailedSubscription
                {
                    DeclinedSubscriptionPurchase = e.DeclinedSubscriptionPurchase,
                    ClientName = $"{client.FirstName} {client.LastName}",
                    ContributionName = declinedContributionsDict[e.ContributionId].Title
                };
            });
        }

        public async Task<OperationResult<string>> CreateCheckoutSession(string clientAccountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == clientAccountId);

            var customerStripeAccountId = user.CustomerStripeAccountId;

            return await _stripeService.CreateCheckoutSessionToUpdatePaymentMethod(customerStripeAccountId);
        }

        public async Task<ContributionMetadataViewModel> GetContributionMetadata(string contributionId)
        {
            var contribution = await _contributionRootService.GetOne(contributionId);
            if (contribution == null)
            {
                return null;
            }

            return new ContributionMetadataViewModel
            {
                Title = contribution.Title,
                Description = contribution.WhoIAm,
                Image = contribution.PreviewContentUrls?.FirstOrDefault() ?? string.Empty,
            };
        }

        public async Task<ContributionMetadataViewModel> GetWebsiteLinkMetadata(string coachName) 
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.ProfileLinkName.ToLower() == coachName);
            if (user == null)
            {
                return null;
            }
            var profile = await _unitOfWork.GetRepositoryAsync<ProfilePage>().GetOne(p => p.UserId == user.Id);

            if (profile == null)
                return null;

            return new ContributionMetadataViewModel
            {
                Title = profile.Tagline,
                Description = profile.SubtagLine,
                Image = profile.PrimaryBannerUrl
            };
        }

        public async Task<ContributionBase> Get(string contributionId)
        {
            return await _contributionRootService.GetOne(contributionId);
        }

        public async Task<string> GetContributionIdByRoomId(string roomSid)
        {
            var contribution = await _contributionRootService.GetOne(c => c.RecordedRooms.Contains(roomSid));
            return contribution?.Id;
        }
        public async Task<OperationResult> RemoveAttachmentFromContributionSessionTimes(ContributionBase contribution, string documentId, bool isVideo)
        {
            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            if (contributionVm is SessionBasedContributionViewModel sessionBased)
            {
                var selfPacesSessionOnly = sessionBased.Sessions.Where(m => m.IsPrerecorded == true).SelectMany(m => m.SessionTimes);
                if (isVideo)
                {
                    if (selfPacesSessionOnly.Count() > 0)
                    {
                        var videoattchmentsList = selfPacesSessionOnly.FirstOrDefault(m => m.PrerecordedSession?.Id == documentId);
                        if (videoattchmentsList != null)
                        {
                            videoattchmentsList.PrerecordedSession = null;
                        }

                    }
                }
                else
                {
                    if (selfPacesSessionOnly.Count() > 0)
                    {
                        var documentToRemove = selfPacesSessionOnly.SelectMany(m => m?.Attachments.Where(m => m.Id == documentId)).FirstOrDefault();
                        if (documentToRemove == null)
                        {
                            return OperationResult.Failure($"Unable to find document.");
                        }
                        var sessionTimeAttachements = selfPacesSessionOnly.Where(m => m.Attachments.Contains(documentToRemove)).FirstOrDefault();
                        if (sessionTimeAttachements is null)
                        {
                            return OperationResult.Failure($"Unable to find attachments with documnetId {documentId}.");
                        }
                        var sessionTimeAttachments = sessionTimeAttachements.Attachments;
                        if (sessionTimeAttachments.Count() == 0)
                        {
                            return OperationResult.Failure($"Unable to find Attachments");
                        }
                        sessionTimeAttachments.Remove((Document)documentToRemove);
                    }
                    else
                    {
                        return OperationResult.Failure($"Unable to find Sessions");
                    }
                }

            }
            var contributionWithAttachment = _mapper.Map<ContributionBase>(contributionVm);
            var contributionUpdated =
                await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contributionWithAttachment);
            return OperationResult.Success(string.Empty, _mapper.Map<ContributionBaseViewModel>(contributionUpdated));
        }
        public OperationResult GetAttachmentFromContributionSelfPacedSessions(ContributionBase contribution, string documentId)
        {
            var contributionVm = _mapper.Map<ContributionBaseViewModel>(contribution);
            Document documentToFind = null;
            if (contributionVm is SessionBasedContributionViewModel sessionBased)
            {
                var selfPacesSessionOnly = sessionBased.Sessions.Where(m => m.IsPrerecorded == true).SelectMany(m => m.SessionTimes);
                if (selfPacesSessionOnly.Count() > 0)
                {
                    documentToFind = selfPacesSessionOnly.SelectMany(m => m?.Attachments.Where(m => m.Id == documentId)).FirstOrDefault();
                    if (documentToFind == null)
                    {
                        return OperationResult.Failure($"Unable to find document.");
                    }
                }
                else
                {
                    return OperationResult.Failure($"Unable to find attachments.");
                }
            }
            return OperationResult.Success(string.Empty, documentToFind);
        }
        public async Task<OperationResult<ContributionBaseViewModel>> SubmitUnfinished(string contributionId, string accountId)
        {
            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            var contributionExisted = await _contributionRootService.GetOne(contributionId);

            if (ValidateParameters(contributionId, contributorUser, contributionExisted) is var parametersValidationResult
                && parametersValidationResult.Failed)
            {
                return parametersValidationResult;
            }

            var viewModel = _mapper.Map<ContributionBaseViewModel>(contributionExisted);

            viewModel.Status = null;

            if (await viewModel.ValidateAsync() is var modelValidationResult && !modelValidationResult.IsValid)
            {
                return OperationResult<ContributionBaseViewModel>.ValidationError(modelValidationResult.Errors);
            }

            viewModel.AssignIdsToTimeRanges();

            if (InsertValidations(viewModel) is var insertValidationResult && insertValidationResult.Failed)
            {
                return insertValidationResult;
            }

            var contribution = _mapper.Map<ContributionBase>(viewModel);

            if (contribution.PaymentType == PaymentTypes.Advance)
            {
                contribution.Status = contributorUser.StandardAccountTransfersEnabled ? ContributionStatuses.InReview : ContributionStatuses.InSandbox;
            }
            else
            {
                contribution.Status = contributorUser.TransfersEnabled ? ContributionStatuses.InReview : ContributionStatuses.InSandbox;
            }

            var contributionBase = await _contributionRootService.GetOne(contribution.Id);
            if (viewModel is SessionBasedContributionViewModel vm)
            {
                await UpdateZoomMeetingsInfo(contribution, contributionBase, contributorUser);
            }

            var insertedContributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contributionId, contribution);

            var insertedViewModel = _mapper.Map<ContributionBaseViewModel>(insertedContributionBase);

            insertedViewModel.ConvertAllOwnUtcTimesToZoned(contributorUser.TimeZoneId);

            insertedViewModel.Type = viewModel.Type;

            await SendNotifications(insertedContributionBase);

            //Set Notification Schedule Job for self paced conrent available
            try
            {
                if (insertedContributionBase.Status != ContributionStatuses.Approved)
                {
                    await _fcmService.SetContentAvailableScheduler(insertedContributionBase, accountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while seting up content available notification scheduler");
            }

            //Set Deafult Email Templates
            var emailtemplate = await GetCustomTemplateByContributionId(contributionId);
            if (emailtemplate == null)
            {
                await SetDefaultEmailTemplatesData(accountId, contributionId);
            }

            await AutoApproveIfInviteOnly(insertedContributionBase, accountId);

            ActiveCampaignDeal activeCampaignDeal = new ActiveCampaignDeal();
            var liveContributions = await _contributionRootService.Get(c => c.Status != ContributionStatuses.Draft && c.UserId == contributorUser.Id);
            bool firstContriution = liveContributions?.Count() == 0;
            ActiveCampaignDealCustomFieldOptions acDealOptions = new ActiveCampaignDealCustomFieldOptions()
            {
                CohereAccountId = contributorUser.AccountId,
                ContributionStatus = EnumHelper<ContributionStatus>.GetDisplayValue(ContributionStatus.Live),
                FirstContributionCreationDate = firstContriution ? DateTime.UtcNow.ToString("MM/dd/yyyy") : null,

            };
            _activeCampaignService.SendActiveCampaignEvents(activeCampaignDeal, acDealOptions);

            return OperationResult<ContributionBaseViewModel>.Success("Success", insertedViewModel);

            static OperationResult<ContributionBaseViewModel> ValidateParameters(string contributionId, User contributorUser, ContributionBase contributionExisted)
            {
                if (contributionExisted == null)
                {
                    return OperationResult<ContributionBaseViewModel>.Failure($"Contribution with following Id is not found: {contributionId}");
                }

                if (contributorUser.Id != contributionExisted.UserId)
                {
                    return OperationResult<ContributionBaseViewModel>.Failure("It is not allowed to edit contribution for other author");
                }

                if (contributionExisted.Status != ContributionStatuses.Draft)
                {
                    return OperationResult<ContributionBaseViewModel>.Failure("Only unfinished contribution can be submitted");
                }

                return OperationResult<ContributionBaseViewModel>.Success();
            }
        }

        public async Task<OperationResult<ContributionBaseViewModel>> GetLastUnfinishedAsync(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);
            var contribution = (await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .Get(e => e.UserId == user.Id && e.Status == ContributionStatuses.Draft))
                .OrderBy(e => e.UpdateTime)
                .FirstOrDefault();

            if (contribution is null)
            {
                return OperationResult<ContributionBaseViewModel>.Failure("Contribution not found");
            }

            return OperationResult<ContributionBaseViewModel>.Success(_mapper.Map<ContributionBaseViewModel>(contribution));
        }

        public async Task<OperationResult<ContributionBaseViewModel>> UseAsTemplate(string contributionId, string accountId)
        {
            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(e => e.AccountId == accountId);

            var contributionExisted = await _contributionRootService.GetOne(contributionId);

            if (contributionExisted == null)
            {
                return OperationResult<ContributionBaseViewModel>.Failure($"Contribution with following Id is not found: {contributionId}");
            }

            if (contributorUser.Id != contributionExisted.UserId)
            {
                return OperationResult<ContributionBaseViewModel>.Failure("It is not allowed to edit contribution for other author");
            }

            contributionExisted.Id = null;
            contributionExisted.Title += " (from template)";
            contributionExisted.Status = ContributionStatuses.Draft;
            contributionExisted.CleanSessions();

            var insertedContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Insert(contributionExisted);

            var viewModel = _mapper.Map<ContributionBaseViewModel>(insertedContribution);

            return OperationResult<ContributionBaseViewModel>.Success(viewModel);
        }

        private async Task SendNotifications(ContributionBase insertedContributionBase)
        {
            if (insertedContributionBase.Status == ContributionStatuses.InReview)
            {
                await _notifictionService.SendContributionStatusNotificationToAuthor(insertedContributionBase);
                await _notifictionService.SendEmailAboutInReviewToAdmins(insertedContributionBase);
            }
        }

        private static bool CheckYSessionTimeBeforeXSessionTimeCrossing(TimeRange x, TimeRange y) =>
            x.EndTime > y.StartTime && x.StartTime < y.EndTime;

        private static bool CheckXSessionTimeBeforeYSessionTimeCrossing(TimeRange x, TimeRange y) =>
            x.EndTime > y.StartTime && x.StartTime < y.StartTime;

        private static bool CheckXSessionTimeInsideYSessionTimeCrossing(TimeRange x, TimeRange y) =>
            (x.EndTime <= y.EndTime && x.StartTime >= y.StartTime)
            || (y.EndTime <= x.EndTime && y.StartTime >= x.StartTime);

        private static bool CheckTimeRangesCrossing(TimeRange x, TimeRange y)
        {
            return
                !x.Equals(y) &&
                (
                    CheckYSessionTimeBeforeXSessionTimeCrossing(x, y)
                    || CheckXSessionTimeBeforeYSessionTimeCrossing(x, y)
                    || CheckXSessionTimeInsideYSessionTimeCrossing(x, y)
                );
        }

        private async Task AutoApproveIfInviteOnly(ContributionBase insertedContributionBase, string accountId)
        {
            try
            {
                var isBeforeApprovedStatuses = insertedContributionBase.Status == ContributionStatuses.InReview
                                               || insertedContributionBase.Status == ContributionStatuses.Revised;

                var isAutoApprove = insertedContributionBase.InvitationOnly && isBeforeApprovedStatuses;
                if (!isAutoApprove)
                {
                    return;
                }

                var admin = (await _unitOfWork.GetRepositoryAsync<Account>().Get(e => e.Roles.Contains(Roles.Admin))).FirstOrDefault();
                if (admin == null)
                {
                    return;
                }

                await ChangeStatusAsync(
                    insertedContributionBase.Id,
                    admin.Id,
                    accountId,
                    new AdminReviewNoteViewModel()
                    {
                        Status = ContributionStatuses.Approved.ToString(),
                        Description = "Auto approved due to invite only"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error during auto approving contribution");
            }
        }

        private OperationResult<ContributionBaseViewModel> InsertValidations(ContributionBaseViewModel viewModel)
        {
            var paymentMethodValidationResult = ValidaterPaymentMethod(viewModel);
            if (paymentMethodValidationResult.Failed)
            {
                return paymentMethodValidationResult;
            }

            var overlappingsValidationResult = ValidateSlotsForOverlappings(viewModel);
            if (overlappingsValidationResult.Failed)
            {
                return overlappingsValidationResult;
            }

            return OperationResult<ContributionBaseViewModel>.Success();
        }

        private static OperationResult<ContributionBaseViewModel> ValidateSlotsForOverlappings(ContributionBaseViewModel viewModel)
        {
            List<TimeRange> incomingBusyTimes = GetIncomingBusyTimes(viewModel);

            if (incomingBusyTimes.Any(x => incomingBusyTimes.Any(y => CheckTimeRangesCrossing(x, y))))
            {
                return OperationResult<ContributionBaseViewModel>.Failure("Your availability time frame windows are overlapping. Please fix this on step 3");
            }

            return OperationResult<ContributionBaseViewModel>.Success();
        }

        private OperationResult<ContributionBaseViewModel> ValidaterPaymentMethod(ContributionBaseViewModel viewModel)
        {
            if (viewModel.PaymentInfo.Cost.HasValue)
            {
                var grossAmount = _paymentSystemFeeService.CalculateGrossAmount(
                    viewModel.PaymentInfo.Cost.Value * _stripeService.SmallestCurrencyUnit,
                    viewModel.PaymentInfo.CoachPaysStripeFee, viewModel.UserId);

                if (grossAmount > _maxAllowedCostAmount)
                {
                    return OperationResult<ContributionBaseViewModel>.Failure($"Contribution cost must be less or" +
                                                   $" equal to {_maxAllowedCostAmount / _stripeService.SmallestCurrencyUnit} {viewModel.DefaultCurrency}");
                }
            }
            else if (viewModel is ContributionMembershipViewModel || viewModel is ContributionCommunityViewModel)
            {
                return OperationResult<ContributionBaseViewModel>.Success();
            }
            else if (!viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription.ToString()) &&
                        (!viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free.ToString())) &&
                        (viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.Free.ToString()) && viewModel.PaymentInfo.PaymentOptions?.Count() > 1))
            {
                return OperationResult<ContributionBaseViewModel>.Failure("Contribution can be empty only for Monthly subscription");
            }

            return OperationResult<ContributionBaseViewModel>.Success();
        }

        private bool HasChat(ContributionBase contributionExisted)
        {
            return contributionExisted.Chat != null;
        }

        private static bool IsContributionTitleOrImageChanged(ContributionBase contributionExisted, ContributionBase contribution)
        {
            return contribution.PreviewContentUrls.FirstOrDefault() != contributionExisted.PreviewContentUrls.FirstOrDefault() ||
                            contribution.Title != contributionExisted.Title;
        }

        private async Task<OperationResult> ValidateMonthlySessionSubscriptionOptions(ContributionBaseViewModel viewModel, ContributionBase contributionExisted)
        {
            if (viewModel.PaymentInfo.MonthlySessionSubscriptionInfo?.Duration != contributionExisted.PaymentInfo.MonthlySessionSubscriptionInfo?.Duration)
            {
                return OperationResult.Failure("After a Contribution is approved, it’s not possible to change the duration (i.e. number of months) of a Monthly Session Subscription.");
            }

            if (viewModel.PaymentInfo.MonthlySessionSubscriptionInfo?.SessionCount != contributionExisted.PaymentInfo.MonthlySessionSubscriptionInfo?.SessionCount)
            {
                return OperationResult.Failure("Session counts cannot be changed for Contributions that have monthly session subscription enabled. If you need to change your session count, please create a new Contribution.");
            }

            if (viewModel.PaymentInfo.MonthlySessionSubscriptionInfo?.MonthlyPrice != contributionExisted.PaymentInfo.MonthlySessionSubscriptionInfo?.MonthlyPrice)
            {
                return OperationResult.Failure("Pricing information cannot be changed if monthly session subscription was enabled when you created this Contribution. If you need to change your pricing, please create a new Contribution.");
            }

            if (await IsMonthlySessionSubscriptionDisablingNotAllowed(viewModel, contributionExisted))
            {
                return OperationResult.Failure("Sorry, it's not allowed to disable Monthly Session Subscription after the contribution has been paid");
            }

            return OperationResult.Success();
        }

        private async Task<OperationResult> ValidateSplitPaymentsOptions(ContributionBaseViewModel viewModel, ContributionBase contributionExisted)
        {
            if (viewModel.PaymentInfo.Cost != contributionExisted.PaymentInfo.Cost)
            {
                return OperationResult.Failure("After a Contribution has been created with split payments enabled, it’s not possible to change the price. If needed, please create a new Contribution.");
            }

            if (viewModel.PaymentInfo.SplitNumbers != contributionExisted.PaymentInfo.SplitNumbers)
            {
                return OperationResult.Failure("Sorry, it's not allowed to edit number of payments for approved contribution within split payments options checked");
            }

            if (viewModel.PaymentInfo.SplitPeriod != contributionExisted.PaymentInfo.SplitPeriod.ToString())
            {
                return OperationResult.Failure("Sorry, it's not allowed to edit split period for approved contribution within split payments options checked");
            }

            if (await IsSplitPaymentDisablingNotAllowed(viewModel, contributionExisted))
            {
                return OperationResult.Failure("Sorry, it's not allowed to disable split payments after the contribution has been paid");
            }

            return OperationResult.Success();
        }

        private async Task UpdateOneToOneClientsCalendarEventsLocation(ContributionOneToOne updatedOneToOne)
        {
            var allNotCompletedEvents = updatedOneToOne.GetAvailabilityTimes().Values.Where(e => !e.BookedTime.IsCompleted);

            var sessionsByParticipant = allNotCompletedEvents.GroupBy(e => e.BookedTime.ParticipantId);

            var newLocation = updatedOneToOne.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedOneToOne.Id));

            foreach (var sessions in sessionsByParticipant)
            {
                try
                { 
                    await _notifictionService.SendOneToOneCourseSessionEditedNotificationToClientAsync(updatedOneToOne.Id, updatedOneToOne.Title, sessions.Key, newLocation, sessions.ToList(), updatedOneToOne.CustomInvitationBody);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private async Task UpdateCoachCalendarEventsLocation(ContributionOneToOne updatedOneToOne)
        {
            var allNotCompletedEvents = updatedOneToOne.GetAvailabilityTimes().Values.Where(e => !e.BookedTime.IsCompleted);

            var newLocationUrl = updatedOneToOne.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedOneToOne.Id));

            try
            {
                await _notifictionService.SendOneToOneCourseSessionEditedNotificationToCoachAsync(updatedOneToOne.Id, updatedOneToOne.Title, updatedOneToOne.UserId, newLocationUrl, allNotCompletedEvents.ToList(), updatedOneToOne.CustomInvitationBody);
            }
            catch (Exception)
            {

            }
        }

        private async Task FillPodsForSessionContribution(ContributionBaseViewModel contributionVm)
        {
            if (contributionVm is SessionBasedContributionViewModel vm)
            {
                var podIds = vm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                vm.Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();

            }
        }

        private async Task<bool> HasSucceededPayment(string contributionId)
        {
            var purchases = await _unitOfWork.GetRepositoryAsync<Purchase>().Get(p => p.ContributionId == contributionId);
            var purchaseVms = _mapper.Map<List<PurchaseViewModel>>(purchases);
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(p => p.Id == contributionId);
            var contributionStandardAccountDic = await _commonService.GetUsersStandardAccountIdsFromPurchases(purchaseVms);
            purchaseVms.ForEach(p => p.FetchActualPaymentStatuses(contributionStandardAccountDic));
            return purchaseVms != null && purchaseVms.Any(p => p.HasSucceededPayment);
        }

        private async Task<bool> IsSplitPaymentDisablingNotAllowed(ContributionBaseViewModel viewModel, ContributionBase contributionExisted)
        {
            if (contributionExisted is ContributionCourse contributionCourse &&
                !viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments.ToString()) &&
                contributionExisted.PaymentInfo.PaymentOptions.Contains(PaymentOptions.SplitPayments))
            {
                var podIds = contributionCourse.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
                var pods = await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id));

                if (contributionCourse.Sessions.SelectMany(e => e.SessionTimes).Any(e => e.ParticipantsIds.Count > 0)
                    || pods.Any(x => x.ClientIds.Count > 0))
                {
                    return true;
                }

                return await HasSucceededPayment(contributionExisted.Id);
            }

            return false;
        }

        private async Task<bool> IsMonthlySessionSubscriptionDisablingNotAllowed(ContributionBaseViewModel viewModel, ContributionBase contributionExisted)
        {
            if (contributionExisted is ContributionOneToOne contributionOneToOne
                && !viewModel.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription.ToString())
                && contributionExisted.PaymentInfo.PaymentOptions.Contains(PaymentOptions.MonthlySessionSubscription))
            {
                return await HasSucceededPayment(contributionExisted.Id);
            }
            return false;
        }

        private async Task UpdateZoomMeetingsInfo(ContributionBase contribution, ContributionBase contributionBase, User requesterUser)
        {
            if (contributionBase is SessionBasedContribution existedCourse && contribution is SessionBasedContribution updatedCourse)
            {
                if (updatedCourse.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom
                    && existedCourse.LiveVideoServiceProvider.ProviderName != Constants.LiveVideoProviders.Zoom)
                {
                    await _zoomService.ScheduleMeetings(contribution, requesterUser, updatedCourse);
                }

                if (updatedCourse.LiveVideoServiceProvider.ProviderName != Constants.LiveVideoProviders.Zoom
                    && existedCourse.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom)
                {
                    foreach (var session in updatedCourse.Sessions)
                    {
                        foreach (var sessionTime in session.SessionTimes.Where(x => x.ZoomMeetingData != null))
                        {
                            await _zoomService.DeleteMeeting(sessionTime.ZoomMeetingData.MeetingId, requesterUser.AccountId);
                            contribution.ZoomMeetigsIds = new List<long>();
                            sessionTime.ZoomMeetingData.JoinUrl = null;
                            sessionTime.ZoomMeetingData.StartUrl = null;
                        }
                    }
                }

                if (updatedCourse.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom
                    && existedCourse.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom)
                {
                    await _zoomService.ScheduleOrUpdateMeetings(contribution, requesterUser, updatedCourse, existedCourse);
                } 
                foreach (var session in existedCourse.Sessions)
                {
                    if (updatedCourse.Sessions.FirstOrDefault(x => x.Id == session.Id) == null)
                    {
                        foreach (var sessionTime in session.SessionTimes.Where(x => x.ZoomMeetingData != null))
                        {
                            await _zoomService.DeleteMeeting(sessionTime.ZoomMeetingData.MeetingId, requesterUser.AccountId);
                        }
                    }
                    else
                    {
                        foreach (var sessionTime in session.SessionTimes)
                        {
                            var updatedSessionTime = updatedCourse.Sessions.FirstOrDefault(x => x.Id == session.Id)?.SessionTimes?.FirstOrDefault(x => x.Id == sessionTime.Id);
                            if (updatedSessionTime == null)
                            {
                                await _zoomService.DeleteMeeting(sessionTime.ZoomMeetingData.MeetingId, requesterUser.AccountId);
                            }
                        }
                    }
                }
            }
        }

        private async Task<IEnumerable<Currency>> GetCurrenciesForContribution(string CountryId)
        {
            var userCountry = await _unitOfWork.GetRepositoryAsync<Country>().GetOne(c => c.Id == CountryId);

            var result = await _unitOfWork.GetRepositoryAsync<Currency>().Get(e => (e.CountryCode == userCountry.Alpha2Code) || e.CountryCode == "US");
            result.ToList().ForEach(f => { f.IsUserDefaultCurrency = (f.CountryCode == userCountry.Alpha2Code); });
            return result;
        }
        private bool IsLiveCourseCoachNotificationRequired(EventDiff eventDiff)
        {
            return IsModified(eventDiff) && !IsOnlyDeleted(eventDiff);
        }

        private bool IsOnlyDeleted(EventDiff eventDiff)
        {
            return eventDiff.UpdatedEvents.Count == 0 && eventDiff.CreatedEvents.Count == 0 && eventDiff.CanceledEvents.Count != 0;
        }

        private bool IsModified(EventDiff eventDiff)
        {
            return eventDiff.UpdatedEvents.Count > 0 || eventDiff.CreatedEvents.Count > 0 || eventDiff.CanceledEvents.Count > 0;
        }

        private static List<TimeRange> GetIncomingBusyTimes(ContributionBaseViewModel viewModel)
        {
            List<TimeRange> incomingBusyTimes;

            if (viewModel is SessionBasedContributionViewModel vm)
            {
                incomingBusyTimes = vm.Sessions.Where(x => !x.IsPrerecorded).SelectMany(s => s.SessionTimes)
                    .Select(st => new TimeRange { StartTime = st.StartTime, EndTime = st.EndTime }).ToList();
            }
            else
            {
                incomingBusyTimes = viewModel.CohealerBusyTimeRanges;
            }

            return incomingBusyTimes;
        }
        public async Task<OperationResult> SaveSignoffInfo(SignoffInfoViewModel model, IFormFile file, string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (user != null && model != null)
            {
                var fileExtension = Path.GetExtension(file.FileName);
                var fileKey = $"{accountId}/{Guid.NewGuid()}{fileExtension}";
                var fileUploadResult = await _fileManager.UploadObjectAsync(
               file.OpenReadStream(),
               _s3SettingsOptions.NonPublicBucketName,
               fileKey,
               file.ContentType,
               1,
               true,
               string.Empty,
               string.Empty
               );
                if (!fileUploadResult.Succeeded)
                {
                    return fileUploadResult;
                }
                var entity = _mapper.Map<SignoffInfo>(model);
                entity.AccountId = accountId;
                await _unitOfWork.GetRepositoryAsync<SignoffInfo>().Insert(entity);
                return OperationResult.Success("Data Saved.");
            }
            return OperationResult.Failure("Error Saving Data");
        }
        public async Task<OperationResult> GetCoachContributionsForZapier(string accountId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            List<ContributionModel> zapierContributionList = new List<ContributionModel>();
            if (user != null)
            {
                var contributions = await _contributionRootService.Get(c => c.UserId == user.Id && c.Status == ContributionStatuses.Approved);
                if (contributions.Count() > 0)
                {
                    foreach(var m in contributions)
                    {
                        var model = new ContributionModel();
                        model.Id = m.Id;
                        model.ContributionName = m.Title;
                        model.CreateDateTime = m.CreateTime;
                        zapierContributionList.Add(model);
                    }
                }
                return OperationResult.Success(string.Empty, zapierContributionList.OrderByDescending(m=>m.CreateDateTime)) ;
            }
            return OperationResult.Failure("Error getting data.");
        }

        public async Task<OperationResult> AddOrUpdateCustomTemplate(EmailTemplatesViewModel emailTemplatesviewModel, string accountId)
        {
            var contributorUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            if (contributorUser == null)
            {
                return OperationResult.Failure("No user found against accountId:" + accountId);
            }
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == emailTemplatesviewModel.ContributionId);
            if (contribution == null)
            {
                return OperationResult.Failure("No contribution found against Id:" + emailTemplatesviewModel.ContributionId);
            }
            //Get template text with type name
            foreach (var template in emailTemplatesviewModel.CustomTemplates)
            {
                var defaultTemplate = typeof(Constants.TemplatesPaths.Contribution).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(a => a.Name == template.EmailType).FirstOrDefault();
                var defaultTemplate2 = typeof(Constants.TemplatesPaths.Communication).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Where(a => a.Name == template.EmailType).FirstOrDefault();
                if (defaultTemplate == null && defaultTemplate2 == null)
                {
                    return OperationResult.Failure("No Template Found Against :" + template.EmailType);
                }
            }
            var emailtemplate = await GetCustomTemplateByContributionId(contribution.Id);
            var entity = _mapper.Map<EmailTemplates>(emailTemplatesviewModel);
            if (emailtemplate == null)
            {
                await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Insert(entity);
            }
            else
            {
                await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(emailtemplate.Id, entity);
                return OperationResult.Success("Object Updated");
            }
            return OperationResult.Success("Object Inserted");
        }
        public async Task<EmailTemplates> GetCustomTemplateByContributionId(string contributionId)
        {
            var emailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == contributionId);
            return emailTemplate;
        }
        public async Task<OperationResult> EnableEmailTemplate(string accountId, string contributionId, string emailType, bool IsEnabled)
        {
            var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == accountId);
            if (contributor == null)
            {
                return OperationResult.Failure("Account not found against account Id :" + accountId);
            }
            var emailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == contributionId);
            if (emailTemplate == null)
            {
                return OperationResult.Failure("No contribution found against contribution Id :" + contributionId);
            }
            var customEmail = emailTemplate.CustomTemplates.Where(a => a.EmailType == emailType).FirstOrDefault();
            if (customEmail == null)
            {
                return OperationResult.Failure("No email type found against email type :" + emailType);
            }
            customEmail.IsEmailEnabled = IsEnabled;
            await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(emailTemplate.Id, emailTemplate);
            return OperationResult.Success();
        }
        public async Task<OperationResult> SetDefaultEmailTemplatesData(string accountId, string contributionId)
        {
            List<UniqueKeyWord> NewSale = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                new UniqueKeyWord{Name="COACH FIRST NAME",Value="{cohealerFirstName}"},
                new UniqueKeyWord{Name="CLIENT NAME",Value="{clientName}"},
                new UniqueKeyWord{Name="CLIENT EMAIL",Value="{clientEmail}"},
                new UniqueKeyWord{Name="CURRENCY SYMBOL",Value="{currencySymbol}"},
                new UniqueKeyWord{Name="PAID AMOUNT",Value="{paidAmount}"},
                new UniqueKeyWord{Name="CURRENCY CODE",Value="{currencyCode}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                //new UniqueKeyWord{Name="YEAR",Value="{year}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
            };
            List<UniqueKeyWord> NewFreeSale = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value= "{contributionName}" },
                new UniqueKeyWord{Name="COACH FIRST NAME",Value="{cohealerFirstName}"},
                new UniqueKeyWord{Name="CLIENT NAME",Value="{clientName}"},
                new UniqueKeyWord{Name="CLIENT EMAIL",Value="{clientEmail}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                //new UniqueKeyWord{Name="YEAR",Value="{year}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
            };
            List<UniqueKeyWord> CohealerSessionReminder = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                new UniqueKeyWord{Name="COACH FIRST NAME",Value="{cohealerFirstName}"},
                new UniqueKeyWord{Name="DATE",Value="{date}"},
                new UniqueKeyWord{Name="TIME",Value="{time}"},
                new UniqueKeyWord{Name="TIME ZONE",Value="{timezone}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                //new UniqueKeyWord{Name="YEAR",Value="{year}"},
            };
            List<UniqueKeyWord> ClientSessionReminder = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="CLIENT FIRST NAME",Value="{clientFirstName}"},
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                new UniqueKeyWord{Name="COACH NAME",Value="{cohealerFirstName}"},
                new UniqueKeyWord{Name="DATE",Value="{date}"},
                new UniqueKeyWord{Name="TIME",Value="{time}"},
                new UniqueKeyWord{Name="TIME ZONE",Value="{timezone}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                //new UniqueKeyWord{Name="YEAR",Value="{year}"},
            };
            List<UniqueKeyWord> SendEmailPartnerCoachInvite = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                new UniqueKeyWord{Name="PARTNER COACH NAME",Value="{ownerFirstName}"},
                new UniqueKeyWord{Name="REGISTER LINK",Value="{registerLink}"},
                new UniqueKeyWord{Name="ASSIGN LINK",Value="{assignLink}"},
            };
            List<UniqueKeyWord> ContributionSessionsWasUpdatedNotification = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="SESSIONS DETAILS",Value="{sessionsDetails}"},
                new UniqueKeyWord{Name="TIME ZONE",Value="{timeZoneFriendlyName}"},
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
            };
            List<UniqueKeyWord> OneToOneWasRescheduled = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="SESSIONS DETAILS",Value="{sessionsDetails}"},
                new UniqueKeyWord{Name="TIME ZONE",Value="{timeZoneFriendlyName}"},
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                new UniqueKeyWord{Name="RESCHEDULING NOTES",Value="{reschedulingNotes}"},
            };
            List<UniqueKeyWord> NewRecordingsAvailable = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="RECEIVER NAME",Value="{receiverName}"},
                new UniqueKeyWord{Name="RECORDING TIME",Value="{startRecordingTime}"},
                new UniqueKeyWord{Name="TIME ZONE",Value="{userTimezone}"},
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                new UniqueKeyWord{Name="FILE NAME",Value="{fileName}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
            };
            List<UniqueKeyWord> UserWasTaggedNotification = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="MENTIONED NAME",Value="{mentionAuthorUserName}"},
                new UniqueKeyWord{Name="CONTRIBUTION NAME",Value="{contributionName}"},
                //new UniqueKeyWord{Name="REPLY LINK",Value="{replyLink}"},
                new UniqueKeyWord{Name="MENTION DATE",Value="{mentionDate}"},
                new UniqueKeyWord{Name="MESSAGE",Value="{message}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
            };
            List<UniqueKeyWord> UnreadConversationGeneral = new List<UniqueKeyWord>()
            {
                new UniqueKeyWord{Name="RECEIVER NAME",Value="{firstName}"},
                //new UniqueKeyWord{Name="LOGIN LINK",Value="{loginLink}"},
                //new UniqueKeyWord{Name="UNSUBSCRIBE LINK",Value="{unsubscribeEmailLink}"},
                new UniqueKeyWord{Name="YEAR",Value="{year}"},
            };
            List<CustomTemplate> defaultTemplates = new List<CustomTemplate> {
                new CustomTemplate
                { Name="Someone joins a Paid Contribution for (COACH)", EmailSubject="Congrats! Payment of {currencySymbol}{paidAmount} from {clientName}",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.NewSale),EmailType =nameof(Constants.TemplatesPaths.Contribution.NewSale),IsEmailEnabled=true, Category="My Email Notification", UniqueKeyWords= NewSale },
                new CustomTemplate
                { Name="Someone joins a Free Contribution for (COACH)", EmailSubject="Congrats!  {clientEmail} just enrolled",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.NewFreeSale),EmailType =nameof(Constants.TemplatesPaths.Contribution.NewFreeSale),IsEmailEnabled=true, Category="My Email Notification", UniqueKeyWords= NewFreeSale },
                new CustomTemplate
                { Name="Coach Upcoming Session Reminder", EmailSubject="Upcoming Session Reminder",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.CohealerSessionReminder),EmailType=nameof(Constants.TemplatesPaths.Contribution.CohealerSessionReminder),IsEmailEnabled=true, Category="My Email Notification",UniqueKeyWords = CohealerSessionReminder},
                new CustomTemplate
                { Name="One Hour Session Reminder", EmailSubject="Upcoming Session Reminder",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientSessionOneHourReminder),EmailType=nameof(Constants.TemplatesPaths.Contribution.ClientSessionOneHourReminder),IsEmailEnabled=true, Category="Sessions and Content", UniqueKeyWords = ClientSessionReminder },
                new CustomTemplate
                { Name="Twenty Four Hour Session Reminder", EmailSubject="Upcoming Session Reminder",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.ClientSessionReminder),EmailType=nameof(Constants.TemplatesPaths.Contribution.ClientSessionReminder),IsEmailEnabled=true, Category="Sessions and Content", UniqueKeyWords = ClientSessionReminder },
                new CustomTemplate
                { Name="Partner Coach Invite", EmailSubject="Join the team",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.SendEmailPartnerCoachInvite),EmailType=nameof(Constants.TemplatesPaths.Contribution.SendEmailPartnerCoachInvite),IsEmailEnabled=true, Category="Sessions and Content", UniqueKeyWords=SendEmailPartnerCoachInvite },
                new CustomTemplate
                { Name="Someone joins a Contribution for (CLIENT)", EmailSubject="Confirmed Session(s) for {contributionName}",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.ContributionSessionsWasUpdatedNotification),EmailType=nameof(Constants.TemplatesPaths.Contribution.ContributionSessionsWasUpdatedNotification),IsEmailEnabled=true, Category="Enrollment and Sales", UniqueKeyWords= ContributionSessionsWasUpdatedNotification },
                new CustomTemplate
                { Name="Reschedule One to One Contribution", EmailSubject="Confirmed Session(s) for {contributionName}",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled),EmailType=nameof(Constants.TemplatesPaths.Contribution.OneToOneWasRescheduled),IsEmailEnabled=true, Category="Sessions and Content", UniqueKeyWords= OneToOneWasRescheduled },
                new CustomTemplate
                { Name="Session Recording Available", EmailSubject="Session Recording Available",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.NewRecordingsAvailable),EmailType=nameof(Constants.TemplatesPaths.Contribution.NewRecordingsAvailable),IsEmailEnabled=true, Category="Sessions and Content", UniqueKeyWords=NewRecordingsAvailable },
                new CustomTemplate
                { Name="Tagging In Post", EmailSubject="You were tagged in {contributionName}",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Contribution.UserWasTaggedNotification),EmailType=nameof(Constants.TemplatesPaths.Contribution.UserWasTaggedNotification),IsEmailEnabled=true, Category="Community", UniqueKeyWords=UserWasTaggedNotification },
                //new CustomTemplate
                //{ Name="Unread Conversation", EmailSubject="You Have Unread Conversations",EmailText=await _notifictionService.GetTemplateContent(Constants.TemplatesPaths.Communication.UnreadConversationGeneral),EmailType=nameof(Constants.TemplatesPaths.Communication.UnreadConversationGeneral),IsEmailEnabled=true, Category="Chat", UniqueKeyWords=UnreadConversationGeneral},
            };
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == contributionId);
            EmailTemplatesViewModel emailTemplates = new EmailTemplatesViewModel()
            {
                ContributionId = contributionId,
                UserId = contribution.UserId,
                ContributionName = contribution.Title,
                CustomTemplates = defaultTemplates
            };
            await AddOrUpdateCustomTemplate(emailTemplates, accountId);
            return OperationResult.Success();
        }
        public async Task<OperationResult> UpdateEmailTemplate(string contributionId, CustomTemplate Template)
        {
            var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == contributionId);
            if (contribution == null)
            {
                return OperationResult.Failure("No Contribution found against Id :" + contributionId);
            }
            var emailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == contributionId);
            if (emailTemplate == null)
            {
                return OperationResult.Failure("No Custom Email Template found against contribution Id :" + contributionId);
            }
            var customEmail = emailTemplate.CustomTemplates.Where(a => a.EmailType == Template.EmailType).FirstOrDefault();
            if (customEmail == null)
            {
                return OperationResult.Failure("No email type found against email type :" + Template.EmailType);
            }
            customEmail.Name = Template.Name;
            customEmail.EmailType = Template.EmailType;
            customEmail.EmailSubject = Template.EmailSubject;
            customEmail.EmailText = Template.EmailText;
            customEmail.IsEmailEnabled = Template.IsEmailEnabled;
            customEmail.Category = Template.Category;
            customEmail.UniqueKeyWords = Template.UniqueKeyWords;
            customEmail.IsCustomBrandingColorsEnabled = Template.IsCustomBrandingColorsEnabled;
            if (string.IsNullOrEmpty(emailTemplate.ContributionName))
            {
                emailTemplate.ContributionName = contribution.Title;
            }
            if (!emailTemplate.IsUpdated)
            {
                emailTemplate.IsUpdated = true;
            }
            try
            {
                await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(emailTemplate.Id, emailTemplate);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return OperationResult.Success(String.Empty, emailTemplate);
        }
        public async Task<OperationResult> CopyContributionEmailSettings(string FromContributionId, string ToContributionId)
        {
            var FromContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == FromContributionId);
            var ToContribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == ToContributionId);
            if (FromContribution == null)
            {
                return OperationResult.Failure("No Contribution found against Id :" + FromContributionId);
            }
            if (ToContribution == null)
            {
                return OperationResult.Failure("No Contribution found against Id :" + ToContributionId);
            }
            var FromEmailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == FromContributionId);
            var ToEmailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == ToContributionId);
            if (FromEmailTemplate == null)
            {
                return OperationResult.Failure("No Custom Email Template found against contribution Id :" + FromContributionId);
            }
            if (ToEmailTemplate == null)
            {
                return OperationResult.Failure("No Custom Email Template found against contribution Id :" + ToContributionId);
            }
            ToEmailTemplate.IsBrandingColorsEnabled = FromEmailTemplate.IsBrandingColorsEnabled;
            ToEmailTemplate.CustomTemplates = FromEmailTemplate.CustomTemplates;
            if (!ToEmailTemplate.IsUpdated)
            {
                ToEmailTemplate.IsUpdated = true;
            }
            try
            {
                await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(ToEmailTemplate.Id, ToEmailTemplate);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return OperationResult.Success(String.Empty, ToEmailTemplate);
        }
        public async Task<OperationResult> GetCustomizedContributions(string accountId, string contributionId)
        {
            var user = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == accountId);
            var contributionBase = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == contributionId);
            if (user == null)
            {
                return OperationResult.Failure("No User found against Id :" + accountId);
            }
            if (contributionBase == null)
            {
                return OperationResult.Failure("No Contribution found against Id :" + contributionId);
            }
            var emailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Get(e => e.UserId == user.Id && e.IsUpdated == true && e.ContributionId != contributionId);
            List<EmailTemplates> finalList = new List<EmailTemplates>();
            foreach(var item in emailTemplate)
            {
                if (!string.IsNullOrEmpty(item.ContributionId))
                {
                    var contribution = await _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == item.ContributionId);
                    item.ContributionName = contribution.Title;
                }
                await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(item.Id,item);
                finalList.Add(item);
            }

            return OperationResult.Success(String.Empty, finalList);
        }
        public async Task<IEnumerable<SelfpacedDetails>> DownloadSelfpacedModuleDetails(string contributionId, string accountId)
        {
            var contribution = _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(a => a.Id == contributionId);
            var contributionPurchases = _unitOfWork.GetRepositoryAsync<Purchase>().Get(a => a.ContributionId == contributionId);
            var coachUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            await Task.WhenAll(contribution, coachUser, contributionPurchases);

            if (coachUser.Result.Id != contribution.Result.UserId)
                throw new Exception($"User is not Coach User for Contribution: {contribution.Result.Title}");
            if (contributionPurchases.Result.ToList().Count <= 0)
                throw new Exception($"No Enrollment found for Contribution: {contribution.Result.Title}");

            var clientIds = contributionPurchases.Result.Select(s => s.ClientId).ToList();

            var clientUsers = await _unitOfWork.GetRepositoryAsync<User>().Get(a => clientIds.Contains(a.Id));
            var accountIds = clientUsers.Select(s => s.AccountId).ToList();
            var clientAccounts = await _unitOfWork.GetRepositoryAsync<Account>().Get(a => accountIds.Contains(a.Id));

           
            List<SelfpacedDetails> detail = new List<SelfpacedDetails>();

            var selfpacesSession = ((SessionBasedContribution)contribution.Result).Sessions.Where(s => s.IsPrerecorded).ToList();

            List<User> clientUserList = clientUsers.ToList<User>();
            for(int i=0; i < clientUserList.Count(); i++) 
            {
                var user = clientUserList[i];
            
                var entry = new SelfpacedDetails
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = clientAccounts.Where(s => s.Id == user.AccountId).FirstOrDefault()?.Email,
                    ModuleContentList = new List<ModuleAndContent>()
                };
                foreach(var session in selfpacesSession.ToList())
                {
                    foreach(var st in session.SessionTimes) 
                    {
                        entry.ModuleContentList.Add(
                            new ModuleAndContent
                            {
                                ModuleName = session.Name,
                                ContentName = st.SubCategoryName,
                                Status = st.UsersWhoViewedRecording.Contains(user.Id) ? "Completed" : "Incomplete"
                            }
                        );
                    }
                }
                detail.Add(entry);
            }
            return detail;
        }
        public async Task<OperationResult> EnableBrandingColorsOnEmailTemplates(string accountId, string contributionId, bool IsEnabled)
        {
            var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(a => a.AccountId == accountId);
            if (contributor == null)
            {
                return OperationResult.Failure("Account not found against account Id :" + accountId);
            }
            var emailTemplate = await _unitOfWork.GetRepositoryAsync<EmailTemplates>().GetOne(a => a.ContributionId == contributionId);
            if (emailTemplate == null)
            {
                return OperationResult.Failure("No contribution found against contribution Id :" + contributionId);
            }
            emailTemplate.IsBrandingColorsEnabled = IsEnabled;
            foreach(var customTemplate in emailTemplate.CustomTemplates )
            {
                customTemplate.IsCustomBrandingColorsEnabled= IsEnabled;
            }
            await _unitOfWork.GetRepositoryAsync<EmailTemplates>().Update(emailTemplate.Id, emailTemplate);
            return OperationResult.Success();
        }
    }
}
