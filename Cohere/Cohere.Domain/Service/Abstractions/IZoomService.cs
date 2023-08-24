using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ZoomNet.Models;

namespace Cohere.Domain.Service.Abstractions
{
	public interface IZoomService
	{
		Task<ScheduledMeeting> ScheduleMeeting(ContributionBase contribution, Session session, SessionTime sessionTime, Cohere.Entity.Entities.User user);
		Task ScheduleMeetings(ContributionBase contribution, Cohere.Entity.Entities.User requesterUser, SessionBasedContribution updatedCourse);

        Task ScheduleOrUpdateMeetings(ContributionBase contribution, Cohere.Entity.Entities.User requesterUser, SessionBasedContribution updatedCourse, SessionBasedContribution existedCourse);

        Task<ScheduledMeeting> ScheduleMeetingForOneToOne(string name, DateTime EndTime, DateTime StartTime, Cohere.Entity.Entities.User requesterUser);

		Task DeleteMeeting(long meetingId, string authCode);

		Task UpdateMeeting(ContributionBase contribution, Session session, SessionTime sessionTime, Cohere.Entity.Entities.User user);


        Task DisconnectZoom(string userId);

		string GetPresignedUrlForRecording(long meetingId, string fileName, bool asAttachment);

		Task SaveZoomRefreshToken(string refreshToken, string accountId, string redirectUri);

		Task DeauthorizeUser(string user_id);
	}
}
