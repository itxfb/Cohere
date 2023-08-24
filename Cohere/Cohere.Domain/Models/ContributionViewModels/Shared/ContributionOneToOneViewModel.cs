using System;
using System.Collections.Generic;
using System.Linq;

using Cohere.Domain.Extensions;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Models.ContributionViewModels.ForClient;
using Cohere.Domain.Models.ContributionViewModels.ForCohealer;
using Cohere.Domain.Models.Payment;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Contribution.Recordings;
using Cohere.Entity.Enums.Contribution;
using Cohere.Entity.UnitOfWork;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ContributionOneToOneViewModel : ContributionBaseViewModel
    {
        //private readonly IZoomService _zoomService;
        //private readonly IUnitOfWork _unitOfWork;

        public ContributionOneToOneViewModel(IValidator<ContributionOneToOneViewModel> validator)
            : base(validator)
        {
           
        }


        public List<int> Durations { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public override List<TimeRange> CohealerBusyTimeRanges
        {
            get => BusyTimeRanges = AvailabilityTimes
                .Select(at => new TimeRange { StartTime = at.StartTime, EndTime = at.EndTime })
                .ToList();

            set => BusyTimeRanges = value;
        }

        public List<PackagePurchase> PackagePurchases { get; set; } = new List<PackagePurchase>();

        public List<PackagePurchase> ClientPackages { get; set; } = new List<PackagePurchase>();

        public override bool ArchivingAllowed => !HasIncompletedPackages && !HasIncompletedSessions;
        public List<ParticipantInfo> ParticipantInfos { get; set; }
        private bool HasIncompletedPackages => PackagePurchases?.Any(e => !e.IsCompleted) ?? false;

        private bool HasIncompletedSessions =>
            AvailabilityTimes?.SelectMany(at => at.BookedTimes).Any(e => !e.IsCompleted) ?? false;

        public override ClosestClassForBannerViewModel GetClosestCohealerClassForBanner(string coachTimeZoneId)
        {
            if (Status == ContributionStatuses.Completed.ToString() || !ClassesInfo.Any())
            {
                return null;
            }

            var coachStartOfDay = DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow, coachTimeZoneId).Date;

            var futureBookedTimes = AvailabilityTimes.SelectMany(at => at.BookedTimes)
                .Where(bt =>
                    !bt.IsCompleted && bt.EndTime >=
                    coachStartOfDay)
                .ToList();

            if (futureBookedTimes.Count <= 0)
            {
                return null;
            }

            var closestCohealerSession = futureBookedTimes.OrderBy(bt => bt.StartTime).First();

            var minutesLeft = (int)closestCohealerSession.StartTime.Subtract(DateTime.UtcNow).TotalMinutes;

            if (minutesLeft > Constants.Contribution.Dashboard.MinutesMaxToShowCohealerClosestSessionBanner)
            {
                return null;
            }

            string closestSessionChatSid = default;
            Chat?.CohealerPeerChatSids?.TryGetValue(closestCohealerSession.ParticipantId, out closestSessionChatSid);

            var availabilityTime =
                AvailabilityTimes.FirstOrDefault(at => at.BookedTimes.Contains(closestCohealerSession));

            return new ClosestClassForBannerViewModel
            {
                ContributionId = Id,
                ContributionTitle = Title,
                ContributionType = Type,
                ClassId = closestCohealerSession.Id,
                ClassGroupId = availabilityTime?.Id,
                OneToOneParticipantId = closestCohealerSession.ParticipantId,
                MinutesLeft = minutesLeft < 0 ? 0 : minutesLeft,
                ChatSid = closestSessionChatSid,
                IsRunning = closestCohealerSession.VideoRoomInfo?.IsRunning ?? false,
                StartTime = closestCohealerSession.StartTime,
                ContributionLiveVideoServiceProvider = LiveVideoServiceProvider
            };
        }

        public override ClosestClassForBannerViewModel GetClosestClientClassForBanner(string clientId,
            string coachTimeZoneId)
        {
            if (Status == ContributionStatuses.Completed.ToString() || !ClassesInfo.Any())
            {
                return null;
            }

            var clientBookedTimes = AvailabilityTimes
                .SelectMany(s => s.BookedTimes)
                .Where(st => st.ParticipantId == clientId)
                .ToList();

            if (!clientBookedTimes.Any())
            {
                return null;
            }

            var coachStartOfDay = DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow, coachTimeZoneId).Date;

            var futureBookedTimes = clientBookedTimes
                .Where(bt =>
                    !bt.IsCompleted && DateTimeHelper.GetZonedDateTimeFromUtc(bt.EndTime, coachTimeZoneId) >=
                    coachStartOfDay)
                .ToList();

            if (futureBookedTimes.Count <= 0)
            {
                return null;
            }

            var closestClientSession = futureBookedTimes.OrderBy(bt => bt.StartTime).First();

            var minutesLeft = (int)closestClientSession.StartTime.Subtract(DateTime.UtcNow).TotalMinutes;

            if (minutesLeft > Constants.Contribution.Dashboard.MinutesMaxToShowClientClosestSessionBanner)
            {
                return null;
            }

            string closestSessionChatSid = default;
            Chat?.CohealerPeerChatSids?.TryGetValue(closestClientSession.ParticipantId, out closestSessionChatSid);

            var availabilityTime =
                AvailabilityTimes.FirstOrDefault(at => at.BookedTimes.Contains(closestClientSession));

            return new ClosestClassForBannerViewModel
            {
                AuthorUserId = UserId,
                ContributionId = Id,
                ContributionTitle = Title,
                ContributionType = Type,
                ClassId = closestClientSession.Id,
                ClassGroupId = availabilityTime?.Id,
                MinutesLeft = minutesLeft < 0 ? 0 : minutesLeft,
                ChatSid = closestSessionChatSid,
                IsRunning = closestClientSession.VideoRoomInfo?.IsRunning ?? false,
                ContributionLiveVideoServiceProvider = LiveVideoServiceProvider
            };
        }

        public override void AssignIdsToTimeRanges()
        {
            AvailabilityTimes.ForEach(availabilityTime =>
            {
                if (string.IsNullOrEmpty(availabilityTime.Id))
                {
                    availabilityTime.Id = Guid.NewGuid().ToString();
                }
            });
        }

        public OperationResult AssignUserToContributionTime(BookTimeBaseViewModel bookModel, string userId,
                    IEnumerable<AvailabilityTime> availabilityTimesWithScheduledSlots, string coachTimeZoneId , string coachAccountId)
        {
            if (!(bookModel is BookOneToOneTimeViewModel bookOneToOneTimeModel))
            {
                return OperationResult.Failure("Unable to book one to one time. Wrong book model type provided");
            }

            IEnumerable<string> assignModelAvailabilityTimesIds = new List<string>
            {
                bookOneToOneTimeModel.AvailabilityTimeId
            };

            var existingAvailabilityTimeIds = availabilityTimesWithScheduledSlots.Select(s => s.Id);

            var matchedAvailabilityTimeIds = assignModelAvailabilityTimesIds
                .Where(id => existingAvailabilityTimeIds.Contains(id)).ToList();

            if (matchedAvailabilityTimeIds.Any())
            {
                var nowInCoachTimeZone = DateTimeHelper.GetZonedDateTimeFromUtc(DateTime.UtcNow, coachTimeZoneId);
                var availabilityTime =
                    availabilityTimesWithScheduledSlots.First(at => at.Id == bookOneToOneTimeModel.AvailabilityTimeId);
                if (availabilityTime.StartTime <= nowInCoachTimeZone.Date ||
                    availabilityTime.EndTime <= nowInCoachTimeZone.Date)
                {
                    return OperationResult.Failure(
                        "Not allowed to book time period that has already started or was in past");
                }

                var hasAlreadyBookedAvailabilityTime = availabilityTimesWithScheduledSlots.Any(e =>
                    e.Id == bookOneToOneTimeModel.AvailabilityTimeId && e.BookedTimes.Any());
                if (hasAlreadyBookedAvailabilityTime)
                {
                    return OperationResult.Failure(
                        "Our system is making this time available again since you did not complete your purchase. Please check again in two minutes");
                }

                var bookOneToOneTimeResultModel = new BookOneToOneTimeResultViewModel
                {
                    ContributionId = Id,
                    AvailabilityTimeIdBookedTimeIdPairs = new Dictionary<string, IEnumerable<string>>()
                };

                var clientBookedTimes = availabilityTimesWithScheduledSlots
                    .SelectMany(at => at.BookedTimes)
                    .Where(bt => bt.ParticipantId == userId)
                    .ToList();


                var bookedTimeIds = new List<string>();

                var timeRangeStartTime = availabilityTime.StartTime;
                var timeRangeEndTime = availabilityTime.EndTime;

                var bookTimeSpan = timeRangeEndTime - timeRangeStartTime;
                var availabilityTimeId = availabilityTime.Id;

                if (Durations.Contains((int)bookTimeSpan.Duration().TotalMinutes))
                {
                    int clientBookedTimeHighestIndex = default;
                    if (clientBookedTimes.Any())
                    {
                        clientBookedTimeHighestIndex = clientBookedTimes.Max(bt => bt.SessionIndex);
                    }

                    var bookedTime = new BookedTime
                    {
                        StartTime = timeRangeStartTime,
                        EndTime = timeRangeEndTime,
                        Id = Guid.NewGuid().ToString(),
                        ParticipantId = userId,
                        IsPurchaseConfirmed = false,
                        SessionIndex = clientBookedTimeHighestIndex == default ? 1 : ++clientBookedTimeHighestIndex
                    };

                    //Add meeting object with bookedtime
                   
                    //try
                    //{
                    //    var requesterUser = _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.User>().GetOne(x => x.AccountId == coachAccountId);// meeting with coach account 
                    //    var meeting = _zoomService.ScheduleMeetingForOneToOne("Session", timeRangeEndTime, timeRangeStartTime, requesterUser.Result);

                    //    bookedTime.ZoomMeetingData = new ZoomMeetingData
                    //    {
                    //        MeetingId = meeting.Result.Id,
                    //        JoinUrl = meeting.Result.JoinUrl,
                    //        StartUrl = meeting.Result.StartUrl
                    //    };

                    //}
                    //catch(Exception ex)
                    //{
                    //    Console.Write(ex.Message);
                    //}
                   
                    


                    bookedTimeIds.Add(bookedTime.Id);

                    availabilityTime = AvailabilityTimes.FirstOrDefault(e => e.Id == availabilityTimeId);

                    if (availabilityTime == default)
                    {
                        availabilityTime = availabilityTimesWithScheduledSlots.First(at => at.Id == availabilityTimeId);
                        AvailabilityTimes.Add(availabilityTime);
                    }

                    availabilityTime.BookedTimes.Add(bookedTime);
                }
                else
                {
                    return OperationResult.Failure(
                        "The duration of time to book must be one of predefined durations in contribution");
                }


                bookOneToOneTimeResultModel.AvailabilityTimeIdBookedTimeIdPairs.Add(availabilityTimeId, bookedTimeIds);


                return OperationResult.Success(string.Empty, bookOneToOneTimeResultModel);
            }

            return OperationResult.Failure(
                "Unable to book one-to-on session time(s). The available times Ids provided do not match existed available times");
        }

        public override OperationResult RevokeAssignmentUserToContributionTime(BookTimeBaseViewModel bookModel, UserViewModel user)
        {
            return OperationResult.Failure($"One On One session time reservation cannot be revoked");
        }

        public override void ClearHiddenForClientInfo(string clientUserId = null, PurchaseViewModel purchaseVm = null)
        {
            base.ClearHiddenForClientInfo(clientUserId);

            AvailabilityTimes.CleanClientInfo(clientUserId);

            if (clientUserId != null)
            {
                ClientPackages = PackagePurchases
                    .Where(pp => pp.UserId == clientUserId && !pp.IsCompleted && pp.IsConfirmed).ToList();

                if (ClientPackages.Count > 0)
                {
                    ClientPackages.ForEach(e => e.TransactionId = null);
                    PackagePurchases = ClientPackages;
                    return;
                }
            }

            PackagePurchases = null;
        }

        public override void AssignChatSidForUserContributionPage(string clientUserId)
        {
            if (Chat != null && Chat.CohealerPeerChatSids.Any())
            {
                var userIdChatSidPair = Chat.CohealerPeerChatSids.FirstOrDefault(c => c.Key == clientUserId);
                if (!userIdChatSidPair.Equals(default(KeyValuePair<string, string>)))
                {
                    Chat.Sid = userIdChatSidPair.Value;
                }
            }
        }

        private JourneyClassInfo MapContributionDetailsToJourneyClassInfo(BookedTime bookedTime,
            int completedClassesCount, 
            int totalClassesCount,
            string timeZoneId,
            string timeZoneShortName)
        {
            return new JourneyClassInfo
            {
                ContributionId = Id,
                ClassId = bookedTime?.Id,
                AuthorUserId = UserId,
                PreviewContentUrls = PreviewContentUrls,
                Type = Type,
                ContributionTitle = Title,
                TotalNumberSessions = totalClassesCount,
                Rating = Rating,
                LikesNumber = LikesNumber,
                SessionTimeUtc = bookedTime?.StartTime,
                NumberCompletedSessions = completedClassesCount,
                IsCompleted = bookedTime == null ? false : bookedTime.IsCompleted,
                TimezoneId = timeZoneId,
                TimeZoneShortForm = timeZoneShortName,
            };
        }

        public override JourneyClassesInfosAll GetClassesInfosForParticipant(string participantId, string timeZoneId, string timeZoneShortName)
        {
            var allClasses = new JourneyClassesInfosAll();

            var bookedByParticipantTimes = AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .Where(bt => bt.ParticipantId == participantId)
                .ToList();

            var completedClassesCount = bookedByParticipantTimes.Count(bt => bt.IsCompleted);
            var bookedClassesCount = bookedByParticipantTimes.Count();

            int notBookedClassesFromPackagesCount = 0;
            if (PackagePurchases.Any())
            {
                notBookedClassesFromPackagesCount = PackagePurchases
                    .Where(pp => pp.UserId == participantId && !pp.IsCompleted)
                    .Sum(p => p.FreeSessionNumbers);
            }

            var totalClassesCount = bookedClassesCount + notBookedClassesFromPackagesCount;

            foreach (var availabilityTime in AvailabilityTimes)
            {
                foreach (var bookedTime in availabilityTime.BookedTimes)
                {
                    if (bookedTime.ParticipantId != participantId)
                    {
                        continue;
                    }

                    if (bookedTime.IsCompleted)
                    {
                        allClasses.Past.Add(MapContributionDetailsToJourneyClassInfo(bookedTime, completedClassesCount,
                            totalClassesCount, timeZoneId, timeZoneShortName));
                    }
                    else
                    {
                        if (bookedTime.StartTime > DateTime.UtcNow)
                        {
                            allClasses.Upcoming.Add(MapContributionDetailsToJourneyClassInfo(bookedTime,
                                completedClassesCount, totalClassesCount, timeZoneId, timeZoneShortName));
                        }
                        else
                        {
                            allClasses.Past.Add(MapContributionDetailsToJourneyClassInfo(bookedTime,
                                completedClassesCount, totalClassesCount, timeZoneId, timeZoneShortName));
                        }
                    }
                }
            }

            if (notBookedClassesFromPackagesCount > 0)
            {
                for (var i = 1; i <= notBookedClassesFromPackagesCount; i++)
                {
                    allClasses.NotBooked.Add(new JourneyClassInfo
                    {
                        ContributionId = Id,
                        ClassId = null,
                        AuthorUserId = UserId,
                        PreviewContentUrls = PreviewContentUrls,
                        Type = Type,
                        ContributionTitle = Title,
                        TotalNumberSessions = totalClassesCount,
                        Rating = Rating,
                        LikesNumber = LikesNumber,
                        SessionTimeUtc = null,
                        NumberCompletedSessions = completedClassesCount,
                        TimezoneId = timeZoneId
                    });
                }
            }

            return allClasses;
        }

        public override int GetTotalNumClassesForParticipant(string participantId)
        {
            return ClassesInfo.Values
                .Count(at => at.ParticipantIds.Contains(participantId));
        }

        public override List<ClosestCohealerSessionInfo> GetClosestCohealerSessions(bool fromDashboard = false)
        {
            var sessions = AvailabilityTimes
                .Where(at => at.BookedTimes.Exists(bt => !bt.IsCompleted))
                .Select(at => at.BookedTimes.Where(bt => !bt.IsCompleted).Select(bt =>
                {
                    bool isChatForParticipantExists = false;
                    string chatSid = null;
                    if (Chat != null)
                    {
                        isChatForParticipantExists =
                            Chat.CohealerPeerChatSids.TryGetValue(bt.ParticipantId, out chatSid);
                    }

                    return new ClosestCohealerSessionInfo
                    {
                        Title = null,
                        StartTime = bt.StartTime,
                        ParticipantsIds = new List<string> { bt.ParticipantId },
                        EnrolledTotal = 1,
                        EnrolledMax = 1,
                        ClassId = bt.Id,
                        ClassGroupId = at.Id,
                        ChatSid = isChatForParticipantExists ? chatSid : null,
                        IsCompleted = bt.IsCompleted
                    };
                }))
                .SelectMany(si => si)
                .OrderBy(si => si.StartTime)
                .ToList();

            return sessions;
        }
        public override List<ClosestCohealerSessionInfo> GetCohealerSessions()
        {
            var sessions = AvailabilityTimes
                .Select(at => at.BookedTimes.Select(bt =>
                {
                    bool isChatForParticipantExists = false;
                    string chatSid = null;
                    if (Chat != null)
                    {
                        isChatForParticipantExists =
                            Chat.CohealerPeerChatSids.TryGetValue(bt.ParticipantId, out chatSid);
                    }

                    return new ClosestCohealerSessionInfo
                    {
                        Title = null,
                        StartTime = bt.StartTime,
                        ParticipantsIds = new List<string> { bt.ParticipantId },
                        EnrolledTotal = 1,
                        EnrolledMax = 1,
                        ClassId = bt.Id,
                        ClassGroupId = at.Id,
                        ChatSid = isChatForParticipantExists ? chatSid : null,
                        IsCompleted = bt.IsCompleted
                    };
                }))
                .SelectMany(si => si)
                .OrderBy(si => si.StartTime)
                .ToList();

            return sessions;
        }
        public override List<SessionInfoForReminderViewModel> GetTomorrowSessions(DateTime tomorrowStartMomentUtc,
            DateTime dayAfterTomorrowStartMomentUtc)
        {
            var tomorrowBookedTimes = AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .Where(bt =>
                    !bt.IsCompleted && bt.StartTime >= tomorrowStartMomentUtc &&
                    bt.StartTime <= dayAfterTomorrowStartMomentUtc)
                .ToList();

            List<SessionInfoForReminderViewModel> reminderModels = new List<SessionInfoForReminderViewModel>();

            if (tomorrowBookedTimes.Count > 0)
            {
                foreach (var bookedTime in tomorrowBookedTimes)
                {
                    var reminderModel = new SessionInfoForReminderViewModel
                    {
                        AuthorUserId = UserId,
                        ClientUserId = bookedTime.ParticipantId,
                        ClassId = bookedTime.Id,
                        ClassStartTimeUtc = bookedTime.StartTime,
                        ContributionId = Id,
                        ContributionTitle = Title
                    };
                    reminderModels.Add(reminderModel);
                }
            }

            return reminderModels;
        }

        public override OperationResult SetClassAsCompleted(string classId)
        {
            var bookedTime = AvailabilityTimes.SelectMany(at => at.BookedTimes).FirstOrDefault(bt => bt.Id == classId);

            if (bookedTime is null)
            {
                return OperationResult.Failure($"Unable to find booked time with Id: {classId}");
            }
            // Toggling IsCompleted Flag per request
            bookedTime.IsCompleted = !bookedTime.IsCompleted;
            bookedTime.CompletedDateTime = DateTime.UtcNow;
            return OperationResult.Success(string.Empty, new List<string> { bookedTime.ParticipantId });
        }

        public override OperationResult SetSelfPacedClassAsCompleted(string classId, string clientId)
        {
            throw new NotImplementedException();
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<string> AttachmentsKeys =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .SelectMany(bt => bt.Attachments).Select(e => e.DocumentKeyWithExtension);

        public override void RevokeAllClassesBookedByUseId(string userId)
        {
            var bookedTimes = AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .Where(bt => bt.ParticipantId == userId);

            foreach (var bookedTime in bookedTimes)
            {
                var relatedAvailabilityTime = AvailabilityTimes.First(at => at.BookedTimes.Contains(bookedTime));
                AvailabilityTimes.Remove(relatedAvailabilityTime);
            }
        }

        public override bool IsExistedSessionsModificationAllowed(
            ContributionBase existedContribution,
            out string errorMessage,
            out List<EditedBookingWithClientId> editedBookingsWithClientId,
            out List<DeletedBookingWithClientId> deletedBookingsWithClientId,
            out List<Document> deletedAttachments)
        {
            errorMessage = string.Empty;
            deletedBookingsWithClientId = new List<DeletedBookingWithClientId>();
            editedBookingsWithClientId = new List<EditedBookingWithClientId>();
            deletedAttachments = new List<Document>();

            if (!(existedContribution is ContributionOneToOne existedOneToOne))
            {
                errorMessage =
                    $"The existed contribution has type {existedContribution.Type} which is different from updated contribution type {Type}";
                return false;
            }

            var joinedAvailabilityTimes = existedOneToOne.AvailabilityTimes.Join(AvailabilityTimes, x => x.Id,
                y => y.Id,
                (x, y) => new { ExistingAvailabilityTime = x, IncomingAvailabilityTime = y }).ToList();

            if (AvailabilityTimes.Count(x => x.Id != null) > joinedAvailabilityTimes.Count())
            {
                errorMessage =
                    "Some availability times in edited contributions that looks like existed have different Id from real existed availability times";
                return false;
            }

            var incomingAvTimesWithNotNullIds = AvailabilityTimes.Where(at => at.Id != null).ToList();

            if (existedOneToOne.AvailabilityTimes.Count() > incomingAvTimesWithNotNullIds.Count())
            {
                var existedAvailabilityTimeIds = existedOneToOne.AvailabilityTimes.Select(at => at.Id);
                var deletedAvailabilityTimesIds =
                    existedAvailabilityTimeIds.Except(incomingAvTimesWithNotNullIds.Select(s => s.Id));

                var deletedAvailabilityTimes =
                    existedOneToOne.AvailabilityTimes.Where(at => deletedAvailabilityTimesIds.Contains(at.Id));

                foreach (var deletedAvailabilityTime in deletedAvailabilityTimes)
                {
                    if (!deletedAvailabilityTime.BookedTimes.Any())
                    {
                        continue;
                    }

                    foreach (var deletedBookedTime in deletedAvailabilityTime.BookedTimes)
                    {
                        deletedBookingsWithClientId.Add(new DeletedBookingWithClientId
                        {
                            ContributionId = Id,
                            ContributionTitle = Title,
                            ClassGroupId = deletedAvailabilityTime.Id,
                            ClassGroupName = null,
                            ClassId = deletedBookedTime.Id,
                            ParticipantId = deletedBookedTime.ParticipantId,
                            DeletedStartTime = deletedBookedTime.StartTime
                        });

                        deletedAttachments.AddRange(deletedBookedTime.Attachments);
                    }
                }
            }

            foreach (var atPair in joinedAvailabilityTimes)
            {
                if (!atPair.ExistingAvailabilityTime.BookedTimes.Any())
                {
                    continue;
                }

                var joinedByIdBookedTimes = atPair.ExistingAvailabilityTime.BookedTimes.Join(
                    atPair.IncomingAvailabilityTime.BookedTimes,
                    x => x.Id, y => y.Id,
                    (x, y) => new { ExistingBookedTime = x, IncomingBookedTime = y }).ToList();

                if (atPair.IncomingAvailabilityTime.BookedTimes.Count(x => x != null) > joinedByIdBookedTimes.Count())
                {
                    errorMessage =
                        "Some incoming session times that looks like existed has different Id from real existed session times";
                    return false;
                }

                var incomingBookedTimesWithNotNullIds =
                    atPair.IncomingAvailabilityTime.BookedTimes.Where(st => st.Id != null).ToList();

                if (atPair.ExistingAvailabilityTime.BookedTimes.Count() > incomingBookedTimesWithNotNullIds.Count)
                {
                    var existedBookedTimesIds = atPair.ExistingAvailabilityTime.BookedTimes.Select(st => st.Id);
                    var deletedBookedTimesIds =
                        existedBookedTimesIds.Except(incomingBookedTimesWithNotNullIds.Select(st => st.Id));

                    var deletedBookedTimes =
                        atPair.ExistingAvailabilityTime.BookedTimes.Where(st => deletedBookedTimesIds.Contains(st.Id));

                    foreach (var deletedBookedTime in deletedBookedTimes)
                    {
                        deletedBookingsWithClientId.Add(new DeletedBookingWithClientId
                        {
                            ContributionId = Id,
                            ContributionTitle = Title,
                            ClassGroupId = atPair.ExistingAvailabilityTime.Id,
                            ClassGroupName = null,
                            ClassId = deletedBookedTime.Id,
                            ParticipantId = deletedBookedTime.ParticipantId,
                            DeletedStartTime = deletedBookedTime.StartTime
                        });

                        deletedAttachments.AddRange(deletedBookedTime.Attachments);
                    }
                }

                foreach (var btPair in joinedByIdBookedTimes)
                {
                    if (btPair.IncomingBookedTime.StartTime != btPair.ExistingBookedTime.StartTime)
                    {
                        editedBookingsWithClientId.Add(new EditedBookingWithClientId
                        {
                            ContributionId = Id,
                            ContributionTitle = Title,
                            ClassGroupId = atPair.ExistingAvailabilityTime.Id,
                            ClassGroupName = null,
                            ClassId = btPair.IncomingBookedTime.Id,
                            ParticipantId = btPair.ExistingBookedTime.ParticipantId,
                            NewStartTime = btPair.IncomingBookedTime.StartTime
                        });
                    }
                }
            }

            return true;
        }

        public BookedTime GetBookedTimeByClassId(string classId)
        {
            return AvailabilityTimes?.SelectMany(at => at.BookedTimes)
                .FirstOrDefault(bt => bt.Id == classId);
        }

        public override IEnumerable<string> GetAllIdentitiesInClass(string classId)
        {
            if (!ClassesInfo.ContainsKey(classId))
            {
                throw new ArgumentException("class not found");
            }

            var classInfo = ClassesInfo[classId];

            var identities = new List<string>(classInfo.ParticipantIds) { UserId };
            return identities;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, List<string>> RoomsWithParticipants =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes
                    .SelectMany(bt => bt.RecordingInfos
                        .Select(ri =>
                            new KeyValuePair<string, List<string>>(ri.RoomId, new List<string> { bt.ParticipantId }))))
                .ToDictionary(x => x.Key, x => x.Value);

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, ClassInfo> ClassesInfo =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .ToDictionary(
                    key => key.Id,
                    value => new ClassInfo
                    {
                        IsCompleted = value.IsCompleted,
                        ParticipantIds = new List<string> { value.ParticipantId },
                        StartTime = value.StartTime,
                        RecordingInfos = value.RecordingInfos,
                        VideoRoomContainer = value,
                    });

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<Document> Attachments =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .SelectMany(bt => bt.Attachments);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<VideoRoomInfo> VideoRoomInfos =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .Select(bt => bt.VideoRoomInfo);

        [System.Text.Json.Serialization.JsonIgnore]
        public override IEnumerable<RecordingInfo> RecordingInfos =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes)
                .SelectMany(bt => bt.RecordingInfos);

        [System.Text.Json.Serialization.JsonIgnore]
        public override Dictionary<string, IList<Document>> AttachmentCollection =>
            AvailabilityTimes
                .SelectMany(at => at.BookedTimes
                    .Select(bt => new KeyValuePair<string, IList<Document>>(bt.Id, bt.Attachments)))
                .ToDictionary(e => e.Key, e => e.Value);

        public List<AvailabilityTime> AvailabilityTimes { get; set; } = new List<AvailabilityTime>();
        public List<TimeRange> AvailabilityTimesForUi { get; set; } = new List<TimeRange>();

        public override void ConvertAllOwnZonedTimesToUtc(string timeZoneId)
        {
            foreach (var availabilityTime in AvailabilityTimes)
            {
                availabilityTime.StartTime = DateTimeHelper.GetUtcTimeFromZoned(availabilityTime.StartTime, timeZoneId);
                availabilityTime.EndTime = DateTimeHelper.GetUtcTimeFromZoned(availabilityTime.EndTime, timeZoneId);

                if (availabilityTime.BookedTimes.Any())
                {
                    foreach (var bookedTime in availabilityTime.BookedTimes)
                    {
                        bookedTime.StartTime = DateTimeHelper.GetUtcTimeFromZoned(bookedTime.StartTime, timeZoneId);
                        bookedTime.EndTime = DateTimeHelper.GetUtcTimeFromZoned(bookedTime.EndTime, timeZoneId);

                        if (bookedTime.CompletedDateTime.HasValue)
                        {
                            bookedTime.CompletedDateTime =
                                DateTimeHelper.GetUtcTimeFromZoned(bookedTime.CompletedDateTime.Value, timeZoneId);
                        }
                    }
                }
            }

            foreach (var availabilityTimeForUi in AvailabilityTimesForUi)
            {
                availabilityTimeForUi.StartTime =
                    DateTimeHelper.GetUtcTimeFromZoned(availabilityTimeForUi.StartTime, timeZoneId);
                availabilityTimeForUi.EndTime =
                    DateTimeHelper.GetUtcTimeFromZoned(availabilityTimeForUi.EndTime, timeZoneId);
            }
        }

        public override void ConvertAllOwnUtcTimesToZoned(string timeZoneId)
        {
            foreach (var availabilityTime in AvailabilityTimes)
            {
                availabilityTime.StartTime =
                    DateTimeHelper.GetZonedDateTimeFromUtc(availabilityTime.StartTime, timeZoneId);
                availabilityTime.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(availabilityTime.EndTime, timeZoneId);

                if (availabilityTime.BookedTimes.Any())
                {
                    foreach (var bookedTime in availabilityTime.BookedTimes)
                    {
                        bookedTime.StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(bookedTime.StartTime, timeZoneId);
                        bookedTime.EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(bookedTime.EndTime, timeZoneId);

                        if (bookedTime.CompletedDateTime.HasValue)
                        {
                            bookedTime.CompletedDateTime =
                                DateTimeHelper.GetZonedDateTimeFromUtc(bookedTime.CompletedDateTime.Value, timeZoneId);
                        }
                    }
                }
            }

            foreach (var availabilityTimeForUi in AvailabilityTimesForUi)
            {
                availabilityTimeForUi.StartTime =
                    DateTimeHelper.GetZonedDateTimeFromUtc(availabilityTimeForUi.StartTime, timeZoneId);
                availabilityTimeForUi.EndTime =
                    DateTimeHelper.GetZonedDateTimeFromUtc(availabilityTimeForUi.EndTime, timeZoneId);
            }

            TimeZoneId = timeZoneId;
        }

        public override IEnumerable<CohealerContributionTimeRangeViewModel> GetCohealerContributionTimeRanges(Dictionary<string, string> clients, string contributorTimeZoneId, bool timesInUtc)
        {
            var today = DateTime.UtcNow.Date;

            var bookedTimes = AvailabilityTimes
                                .SelectMany(at => at.BookedTimes)
                                .Where(c => c.StartTime >= today);

            return bookedTimes.Select(bt => new CohealerContributionTimeRangeViewModel()
            {
                ContributionId = Id,
                EventId = AvailabilityTimes.FirstOrDefault(e => e.BookedTimes.Contains(bt))?.Id,
                ContributionType = nameof(ContributionOneToOne),
                SessionName = clients.GetValueOrDefault(bt.ParticipantId, "deleted client"),
                SessionStartTime = timesInUtc ? bt.StartTime : DateTimeHelper.GetZonedDateTimeFromUtc(bt.StartTime, contributorTimeZoneId),
                SessionEndTime = timesInUtc ? bt.EndTime : DateTimeHelper.GetZonedDateTimeFromUtc(bt.EndTime, contributorTimeZoneId),
            });
        }
    }
}
