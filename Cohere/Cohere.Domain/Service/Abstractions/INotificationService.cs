using Cohere.Domain.Models.Payment;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Models.Notification;
using System;
using Ical.Net.CalendarComponents;
using Cohere.Domain.Service.Nylas;
using RestSharp;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Infrastructure;

namespace Cohere.Domain.Service.Abstractions
{
    public interface INotificationService
    {
        Task SendPaymentSuccessNotification(Purchase purchase, ContributionBase contribution, SucceededPaymentEmailViewModel model);

        Task SendClientEnrolledNotification(ContributionBase contribution, string clientName, string clientEmail, string paidAmount);
        Task SendClientFreeEnrolledNotification(ContributionBase contribution, string clientEmail, string clientName);


        Task SendContributionInvitationMessage(ContributionBase contributionToShare, IEnumerable<string> emailAddresses, string inviterAccountId);

        Task SendCustomEmailFromCohealer(string cohealerAccountId, string clientUserId, string customMessage);

        Task SendEmailConfirmationLink(string accountEmail, string emailConfirmationToken, bool isCohealer);

        Task SendPasswordResetLink(string accountId, string accountEmail, string passwordRestorationToken);

        Task NotifyTaggedUsers(UserTaggedNotificationViewModel model);

        Task SendTransferMoneyNotification(string userFirstName, string email, string lastFourDigitsOfBankAccount);

        Task SendContributionStatusNotificationToAuthor(ContributionBase contribution);

        Task SendEmailAboutInReviewToAdmins(ContributionBase contribution);

        Task SendUnreadConversationsNotification(HashSet<string> usersToSendModels);

        Task SendSessionRescheduledNotification(List<EditedBookingWithClientId> editedBookingsWithClientIds, string contributionAuthorFirstName);

        Task SendSessionDeletedNotification(List<DeletedBookingWithClientId> deletedBookingsWithClientIds, string authorFirstName);

        Task SendInstructionsToNewCohealerAsync(string cohealerAccountId);

        Task SendEmailCohealerInstructionGuide(string cohealerEmail, string cohealerFirstName);

        Task SendEmailPartnerCoachInvite(string email, ContributionBase contribution, string assignActionUrl);

        Task SendEmailCohealerOneToOneInstructionGuide(string cohealerEmail, string cohealerFirstName);

        Task SendEmailCohealerShareContributionGuide(ContributionBase contribution);

        Task SendLiveCouseBookSessionNotificationForClientAsync(string liveCourseTitle, string userId, string locationUrl, List<SessionTimeToSession> bookedEvents, string contributorId , string contributionID, bool sendIcalAttachment = true);

        Task SendLiveCourseWasUpdatedNotificationAsync(
            string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserIds,
            string locationUrl,
            EventDiff eventDiff,string contributorId, string contributionID, bool sendIcalAttachment = true);

        Task SendLiveCourseWasUpdatedNotificationAsync(
            string sourceAddress,
            string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserIds,
            string locationUrl,
            EventDiff eventDiff, string contributorId, string contributionID, bool sendIcalAttachment = true);

        Task SendOneToOneCourseSessionBookedNotificationToCoachAsync(
            string contributionId,
            string oneToOneCourseTitle,
            string coachUserId,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> bookedEventsForCoach, string CustomInvitationBody, bool sendIcalAttachment = true);

        Task SendOneToOneCourseSessionBookedNotificationToClientAsync(
            string contributionId,
            string oneToOneCourseTitle,
            string clientUserId,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> bookedEventsForClient, string CustomInvitationBody, bool sendIcalAttachment=true);

        Task SendLiveCourseWasUpdatedNotificationAsync(
            string liveCourseTitle,
            Dictionary<string, bool> coachAndPartnerUserId,
            string locationUrl,
            List<SessionTimeToSession> createdEvents, string contributorId, string contributionID, bool sendIcalAttachment = true);

        Task SendOneToOneReschedulingNotificationToCoach(
            string contributionId,
            string oneToOneCourseTitle,
            string coachUserId,
            string reschedulingNotes,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> rescheduledEventsForCoach, string CustomInvitationBody, bool sendIcalAttachment = true);

        Task SendOneToOneReschedulingNotificationToClient(
            string contributionId,
            string oneToOneCourseTitle,
            string clientUserId,
            string reschedulingNotes,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> rescheduledEventsForClient, string CustomInvitationBody, bool sendIcalAttachment = true);

        Task SendOneToOneCourseSessionEditedNotificationToClientAsync(
            string contributionId,
            string oneToOneCourseTitle,
            string clientUserId,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> editedEventsForClient, string CustomInvitationBody);

        Task SendOneToOneCourseSessionEditedNotificationToCoachAsync(
            string contributionId,
            string oneToOneCourseTitle,
            string coachUserId,
            string locationUrl,
            List<BookedTimeToAvailabilityTime> editedEventsForCoach, string CustomInvitationBody);

        Task SendReferralLinkEmailMessage(IEnumerable<string> emailAddresses, string affiliateAccountId, string inviteCode);

        Task SendCoachJoinedNotificationToAllAdmins(string cohealerEmail, string invitedBy, User cohealerUser);

        Task SendPurchaseFailNotifcationToCoach(string coachEmail, string clientFirstName, string clientLastName, string clientEmail, string errorMessage, string contributionTitle);

        Task SendNotificationAboutUploadedContent(string cohealerAccountId, IEnumerable<string> participantUserIds, string fileName, string contributionName, string downloadLink="", string redirectLink="");

        Task SendNotificationAboutNewRecording(string roomId, List<string> participantUserIds, string fileName, ContributionBaseViewModel contributionBaseVm, string sessionTimeId);

        Task NotifyNewCoach(Account accountAssociated, User userInserted);

        Task SendCohrealerPaidTierAccountCancellationNotificationToAdmins(string accountId, User customerUser, string planeName, string billingFrequency, DateTime? endOfMembershipDate, DateTime? cancellationDate);

        Task SendNotificationBeforeExpirationOfCancelledPlanToAdmins(List<CancelledPlanExpirationEmailModel> modelList);

        Task SendNotificationForFailedPayments(DeclinedSubscriptionPurchase declinedSubscriptionPurchase, User user, string billingFrequency, string planName, DateTime? planStartDate);
     
        Task SendNotificationForNewSignupOfPaidtierAccount(string customerName, string customerEmail, string billingFrequency, string planName, DateTime? accountCreationTime, DateTime? nextRenewelDate);

        Task SendInvoicePaidEmailToCoach(string coachAccountId, string clientEmail, string invoiceNumber, string contributionTitle);
        Task SendInvoiceDueEmailToClient(string clientEmail, string clientFirstName, string contributionTitle, string coachFirstName);
        Task SendInvoiceDueEmailToCoach(string coachAccountId, string clientEmail, string invoiceNumber, string contributionTitle);
        Task SendSessionReminders(DateTime dateTimeStart, DateTime dateTimeEnd, bool sendClientsOnly);

        Task<NylasEventCreation> CreateorUpdateCalendarEvent(CalendarEvent calEvent, string clientid, NylasAccount nylasAccount , BookedTimeToAvailabilityTime bookedTimeToAvailabilityTime, bool isUpdate=false, string EventId="");
        Task<NylasEventCreation> CreateorUpdateCalendarEventForSessionBase(CalendarEvent calEvent, List<string> clientid, NylasAccount nylasAccount, SessionTimeToSession sessionTimeToSession, bool isUpdate = false, string EventId = "");
        Task<bool> DeleteCalendarEventForSessionBase(SessionBasedContribution updatedCourse, string sessionTimeId, string participantId, string eventId = "");
        Task<string> GetTemplateContent(string templatePath);
        Task SendTestEmailNotification(string accountId, CustomTemplate customTemplate);

    }
}
