using System;
using System.Linq;
using Cohere.Domain.Extensions;
using Cohere.Domain.Models.Notification;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Service.Abstractions.BackgroundExecution;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Community;
using Cohere.Entity.Enums.FCM;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Service.BackgroundExecution
{
    public class SchedulePostJob : ISchedulePostJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFCMService _fcmService;
        private readonly ILogger<SendEmailCoachInstructionGuideJob> _logger;
        private readonly INotificationService _notificationService;

        public SchedulePostJob(IUnitOfWork unitOfWork, ILogger<SendEmailCoachInstructionGuideJob> logger, INotificationService notificationService, IFCMService fcmService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
            _fcmService = fcmService;
        }

        public void Execute(params object[] args)
        {
            var jobGuid = Guid.NewGuid();
            _logger.LogInformation($"Started Job {nameof(SchedulePostJob)}. Job Guid: {jobGuid}");
            try
            {
                if (!args.Any())
                {
                    throw new ArgumentException("Args has no elements");
                }

                var postId = args[0] as string;
                if (string.IsNullOrWhiteSpace(postId))
                {
                    throw new ArgumentException("PostId is not provided");
                }

                var postRepo = _unitOfWork.GetGenericRepositoryAsync<Post>();
                var postTask = postRepo.GetOne(x => x.Id == postId);
                postTask.Wait();
                var post = postTask.Result;

                if (post is null)
                {
                    throw new ArgumentException("Post not found");
                }

                if (post.IsScheduled && post.ScheduledTime.HasValue) 
                {
                    post.IsScheduled = false;
                    post.CreateTime =Convert.ToDateTime(post.ScheduledTime);
                    post.ScheduledTime = null;
                    post.ScheduledJobId = null;
                    var updatedPost =  _unitOfWork.GetRepositoryAsync<Post>().Update(post.Id,post,true);
                    try
                    {
                        var IsfirstPost =  _unitOfWork.GetRepositoryAsync<Post>().Get(p => p.UserId == updatedPost.Result.UserId && p.ContributionId == updatedPost.Result.ContributionId);
                        if (IsfirstPost != null)
                        {
                            if (IsfirstPost.Result.Where(x => !x.IsDraft).Count() == 1)
                            {
                                _fcmService.SendPostPushNotification(post);
                            }
                        }
                    }
                    catch
                    {

                    }
                    var autherUser = _unitOfWork.GetGenericRepositoryAsync<User>().GetOne(x => x.Id == post.UserId);
                    var contribution = _unitOfWork.GetGenericRepositoryAsync<ContributionBase>().GetOne(x => x.Id == post.ContributionId);
                    UserTaggedNotificationViewModel obj = new UserTaggedNotificationViewModel()
                    {
                        MentionedUserIds = post.TaggedUserIds,
                        MentionAuthorUserName = autherUser.Result.FirstName + " " + autherUser.Result.LastName,
                        ContributionName = contribution.Result.Title,
                        Message = post.Text,
                        ReplyLink = post.ReplyLink,
                    };
                     _notificationService.NotifyTaggedUsers(obj);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}
