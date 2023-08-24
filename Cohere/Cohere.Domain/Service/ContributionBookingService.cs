using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Nylas;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Enums.Payments;
using Cohere.Entity.UnitOfWork;
using Ical.Net.CalendarComponents;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;
using RestSharp;

namespace Cohere.Domain.Service
{
    public class ContributionBookingService : IContributionBookingService
    {
        private readonly ICommonService _commonService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IContributionRootService _contributionRootService;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly ISynchronizePurchaseUpdateService _synchronizePurchaseUpdateService;
        private readonly ILogger<ContributionBookingService> _logger;

        public ContributionBookingService(
            IUnitOfWork unitOfWork,
            IContributionRootService contributionRootService,
            IMapper mapper,
            INotificationService notificationService,
            ISynchronizePurchaseUpdateService synchronizePurchaseUpdateService,
            ILogger<ContributionBookingService> logger, ICommonService commonService)
        {
            _unitOfWork = unitOfWork;
            _contributionRootService = contributionRootService;
            _mapper = mapper;
            _notificationService = notificationService;
            _synchronizePurchaseUpdateService = synchronizePurchaseUpdateService;
            _logger = logger;
            _commonService = commonService;
        }
        public OperationResult BookSessionTimeAsync(List<BookSessionTimeViewModel> bookModels, string requesterAccountId, int logId)
        {
                bool sendIcalAttachment = true;
                var requesterUser = _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requesterAccountId).Result;
                var requesterUserVm = _mapper.Map<UserViewModel>(requesterUser);
                var existingContribution =  _contributionRootService.GetOne(bookModels.FirstOrDefault()?.ContributionId).Result;

                if (existingContribution == null)
                {
                    _logger.LogError($"Contribution to assign user with id {requesterAccountId} is not found @ logId:{logId}");
                    return OperationResult.Failure("Contribution to assign user is not found");
                }

                var purchase = _unitOfWork.GetRepositoryAsync<Purchase>().GetOne(p => p.ContributionId == existingContribution.Id && p.ClientId == requesterUser.Id).Result;
                var purchaseVm = _mapper.Map<PurchaseViewModel>(purchase);
            var contributionAndStandardAccountIdDic = _commonService.GetStripeStandardAccounIdFromContribution(existingContribution).GetAwaiter().GetResult();
            purchaseVm?.FetchActualPaymentStatuses(contributionAndStandardAccountIdDic);

            if(purchaseVm == null || !purchaseVm.HasSucceededPayment)
                {
                    _logger.LogError($"Unable to book slot in session time for client {requesterAccountId}. Please purchase the contribution first @ logId:{logId}");
                    return OperationResult.Failure("Unable to book slot in session time. Please purchase the contribution first");
                }

                var courseVm = _mapper.Map<SessionBasedContributionViewModel>((SessionBasedContribution)existingContribution);
                FillPodsForSessionContribution(courseVm).GetAwaiter().GetResult();

                OperationResult result = null;
                //Nylas Event creation if External calendar is attached                
                var coach = _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == existingContribution.UserId).Result;
                //var contribution = _unitOfWork.GetRepositoryAsync<ContributionBase>().GetOne(u => u.Id == existingContribution.Id).Result;
                NylasAccount NylasAccount = null;
                if (!string.IsNullOrEmpty(existingContribution.ExternalCalendarEmail))
                    NylasAccount = _unitOfWork.GetRepositoryAsync<NylasAccount>().GetOne(n => n.CohereAccountId == coach.AccountId && n.EmailAddress.ToLower() == existingContribution.ExternalCalendarEmail.ToLower()).Result;
                if (NylasAccount != null && !string.IsNullOrEmpty(existingContribution.ExternalCalendarEmail))
                {
                    sendIcalAttachment = false;
                }
            var nonBookedModel = new List<BookSessionTimeViewModel>();
            foreach (var model in bookModels)
                {
                    result = courseVm.AssignUserToContributionTime(model, requesterUserVm);
                    if (result.Failed)
                    {
                    nonBookedModel.Add(model);
                        _logger.LogError(result.Message, $"error during AssignUserToContributionTime for client {requesterAccountId} @ logId:{logId}");
                        //return OperationResult.Failure(result.Message);
                    }
                    
                try
                {
                    if (!sendIcalAttachment)
                    {
                        try
                        {
                            var groupCourse = _mapper.Map<SessionBasedContribution>(courseVm);
                            var updatedSessionTimeDic = groupCourse.GetSessionTimes($"{coach.FirstName} {coach.LastName}").Where(x => x.Value.SessionTime.Id == model.SessionTimeId).FirstOrDefault();
                            if (updatedSessionTimeDic.Value != null)
                            {
                                var updatedSessionTime = updatedSessionTimeDic.Value;
                                CalendarEvent calevent = _mapper.Map<CalendarEvent>(updatedSessionTime);
                                calevent.Location = groupCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(groupCourse.Id)); ; // Needs to be update
                                calevent.Description = existingContribution.CustomInvitationBody;
                                NylasEventCreation eventResponse = new NylasEventCreation();
                                EventInfo _eventinfo = updatedSessionTime.SessionTime.EventInfos.FirstOrDefault();
                                if (_eventinfo != null)
                                {
                                    //var userIds = _contribution.Sessions.SelectMany(x => x.SessionTimes).SelectMany(x => x.ParticipantsIds).Distinct();
                                    //var users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => userIds.Contains(u.Id));
                                    var ids = updatedSessionTime.SessionTime.ParticipantsIds.Distinct().ToList();
                                    eventResponse = _notificationService.CreateorUpdateCalendarEventForSessionBase(calevent, ids.ToList(), NylasAccount, updatedSessionTime, true, _eventinfo.CalendarEventID).Result;
                                }
                                else
                                {
                                    var ids = new List<string>();
                                    ids.Add(requesterUserVm.Id);
                                    eventResponse = _notificationService.CreateorUpdateCalendarEventForSessionBase(calevent, ids, NylasAccount, updatedSessionTime).Result;
                                }
                                if (string.IsNullOrEmpty(eventResponse.id))
                                {
                                    // sendIcalAttachment = true;
                                }
                                else
                                {
                                    var sessionTime = groupCourse.Sessions.SelectMany(x => x.SessionTimes).Where(x => x.Id == model.SessionTimeId).FirstOrDefault();

                                    EventInfo eventInfo = new EventInfo()
                                    {
                                        CalendarEventID = eventResponse.id,
                                        CalendarId = eventResponse.calendar_id,
                                        NylasAccountId = eventResponse.account_id,
                                        AccessToken = NylasAccount.AccessToken,
                                        ParticipantId = requesterUserVm.Id


                                    };
                                    sessionTime.EventInfos.Add(eventInfo);
                                    //var contrib = _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, _updatedContribution).Result;

                                    sendIcalAttachment = false;
                                    courseVm = _mapper.Map<SessionBasedContributionViewModel>((SessionBasedContribution)groupCourse);

                                }
                            }
                            else
                            {
                                sendIcalAttachment = true;
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during sending Session Base Nylas Invite to client/coach");

                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"error during creating Nylas Events for client {requesterAccountId} @ logId:{logId}");
                }

            }

                var updatedCourse = _mapper.Map<SessionBasedContribution>(courseVm);
                //updatedCourse = SyncContributionWithPuchases(updatedCourse);

                _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(existingContribution.Id, updatedCourse).GetAwaiter().GetResult();
                var bookedEvents = new List<SessionTimeToSession>();
                var sessions = updatedCourse.GetSessionTimes($"{requesterUser.FirstName} {requesterUser.LastName}", withPreRecorded: false);
                foreach (var model in bookModels)
                {
                        AssignBookedTimeToPurchasePayment(model.ContributionId, requesterUser.Id, model.SessionTimeId, logId).GetAwaiter().GetResult();
                        if (sessions.ContainsKey(model.SessionTimeId) && !nonBookedModel.Contains(model))
                        {
                            bookedEvents.Add(sessions[model.SessionTimeId]);
                        }
                }

                try
                {
                    var locationUrl = updatedCourse.LiveVideoServiceProvider.GetLocationUrl(_commonService.GetContributionViewUrl(updatedCourse.Id));
                if(bookedEvents.Any())
                {
                    _notificationService.SendLiveCouseBookSessionNotificationForClientAsync(updatedCourse.Title, requesterUser.Id, locationUrl, bookedEvents, updatedCourse.UserId, updatedCourse.Id, sendIcalAttachment).GetAwaiter().GetResult();
                }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"error during sending booking notification for client {requesterAccountId} @ logId:{logId}");
                }

                return OperationResult.Success("User has been assigned to session time successfully", result.Payload);
            
        }

        private  SessionBasedContribution SyncContributionWithPuchases(SessionBasedContribution contribution)
        {
            var updatedListOfParticipantsOfAllSession = getParticipantsListOfAllSession(contribution).GetAwaiter().GetResult();

            if(updatedListOfParticipantsOfAllSession != null)
                foreach(var session in contribution?.Sessions?.Where(s=> !s.IsPrerecorded && s.SessionTimes?.Count == 1))
                {
                    var sessionTime = session.SessionTimes.FirstOrDefault();
                    if (sessionTime != null && !sessionTime.IsCompleted)
                    {
                        updatedListOfParticipantsOfAllSession.TryGetValue(sessionTime.Id, out var participantList);

                        if (participantList != null && participantList.Count > 0)
                        {
                            var remainingSpaceForParticipant = (int)session.MaxParticipantsNumber - sessionTime.ParticipantsIds.Count();
                            if (remainingSpaceForParticipant >= participantList.Count()) //enough space to add all participants in the list
                                 sessionTime.ParticipantsIds?.AddRange(participantList);
                            else if (remainingSpaceForParticipant > 0)
                                 sessionTime.ParticipantsIds?.AddRange(participantList.Take(remainingSpaceForParticipant));
                        }
                    }
                }
                return contribution;
        }

        private async Task<Dictionary<string,List<string>>> getParticipantsListOfAllSession(SessionBasedContribution contribution)
        {
            var purchases = await _unitOfWork.GetGenericRepositoryAsync<Purchase>().Get(p => p.ContributionId == contribution.Id);
            var listOfParticipantsofAllSessions = new Dictionary<string, List<string>>();
            
            if(purchases != null)
                foreach(var session in contribution.Sessions.Where(s=> !s.IsPrerecorded && s.SessionTimes.Count() == 1))
                {
                    var sessionTime = session?.SessionTimes?.FirstOrDefault();
                    if (sessionTime != null)
                    {
                        var listofParticipantsOfEachSession = new List<string>();
                        foreach(var purchase in purchases)
                        {
                            if (!purchase.Payments.FirstOrDefault().BookedClassesIds.Contains(sessionTime.Id))
                                listofParticipantsOfEachSession.Add(purchase.ClientId);
                        }
                        listOfParticipantsofAllSessions.Add(sessionTime.Id, listofParticipantsOfEachSession);
                    }
                }
                return listOfParticipantsofAllSessions;
        }
        public async Task<OperationResult> RevokeBookingOfSessionTimeAsync(BookSessionTimeViewModel bookModel, string requesterAccountId, SessionBasedContribution existedCourse = null)
        {
            var requesterUser = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == requesterAccountId);

            var requesterUserVm = _mapper.Map<UserViewModel>(requesterUser);

             
                
            var existingContribution = await _contributionRootService.GetOne(bookModel.ContributionId);
            //if (existedCourse != null)
            //    existingContribution = existedCourse;

            if (existingContribution == null)
            {
                return OperationResult.Failure("Contribution to revoke reservation is not found");
            }

            var courseVm = _mapper.Map<SessionBasedContributionViewModel>((SessionBasedContribution) existingContribution);
            await FillPodsForSessionContribution(courseVm);

            var result = courseVm.RevokeAssignmentUserToContributionTime(bookModel, requesterUserVm);
            if (!result.Succeeded)
            {
                return OperationResult.Failure(result.Message);
            }

            var updatedCourse = _mapper.Map<SessionBasedContribution>(courseVm);
            await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                .Update(existingContribution.Id, updatedCourse);
            await RevokeBookedTimeFromPurchasePayment(bookModel.ContributionId, requesterUser.Id, bookModel.SessionTimeId);

            //Delete Nylas event if same external calendar is attached 
            bool response = await _notificationService.DeleteCalendarEventForSessionBase(updatedCourse, bookModel.SessionTimeId, requesterUser.AccountId);
            //if(response== ResponseStatus.Error)
            //{
            //    _logger.LogError("Error during sending Session Base Nylas Invite to client/coach");

            //}


            return OperationResult.Success("User revoked session time reservation successfully");
        }

        private async Task FillPodsForSessionContribution(SessionBasedContributionViewModel courseVm)
        {
            var podIds = courseVm.Sessions.SelectMany(x => x.SessionTimes).Where(x => !string.IsNullOrEmpty(x.PodId)).Select(x => x.PodId);
            courseVm.Pods = (await _unitOfWork.GetRepositoryAsync<Pod>().Get(x => podIds.Contains(x.Id))).ToList();
        }

        private async Task AssignBookedTimeToPurchasePayment(string contributionId, string clientId, string classId, int logId)
        {          
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(p => p.ContributionId == contributionId && p.ClientId == clientId);

            switch (purchase.ContributionType)
            {
                case nameof(ContributionCourse):
                case nameof(ContributionCommunity):
                case nameof(ContributionMembership):
                    var reqAcc = await _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(u => u.Id == clientId);
                    _logger.LogInformation($"AssignBookedTimeToPurchasePayment for cleint : {reqAcc.AccountId} @ logId:{logId}");

                    foreach (var payment in purchase.Payments.Where(p => p.PaymentStatus == PaymentStatus.Succeeded || p.PaymentStatus == PaymentStatus.Paid))
                    {
                        payment.BookedClassesIds.Add(classId);
                    }

                    _synchronizePurchaseUpdateService.Sync(purchase);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task RevokeBookedTimeFromPurchasePayment(string contributionId, string clientId, string classId)
        {
            var purchase = await _unitOfWork.GetRepositoryAsync<Purchase>()
                .GetOne(p => p.ContributionId == contributionId && p.ClientId == clientId);

            switch (purchase.ContributionType)
            {
                case nameof(ContributionCourse):
                case nameof(ContributionCommunity):
                case nameof(ContributionMembership):
                    foreach (var payment in purchase.Payments.Where(p => p.HasBookedClassId(classId)))
                    {
                        payment.BookedClassesIds.Remove(classId);
                    }

                    _synchronizePurchaseUpdateService.Sync(purchase);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
