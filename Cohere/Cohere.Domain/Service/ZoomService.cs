using Amazon.S3;
using Amazon.S3.Model;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.EntitiesAuxiliary;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.EntitiesAuxiliary.Zoom;
using Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks;
using Cohere.Entity.Infrastructure.Options;
using Cohere.Entity.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZoomNet;
using ZoomNet.Models;

namespace Cohere.Domain.Service
{
    public class ZoomService : IZoomService
    {
        private readonly ZoomSettings _zoomOptions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly S3Settings _s3SettingsOptions;
        private readonly IAmazonS3 _amazonS3;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ZoomService> _logger;

		public ZoomService(IOptions<ZoomSettings> zoomOptions, IUnitOfWork unitOfWork, IOptions<S3Settings> s3SettingsOptions, IAmazonS3 amazonS3, IMemoryCache memoryCache, ILogger<ZoomService> logger)
		{
			_zoomOptions = zoomOptions?.Value;
			_unitOfWork = unitOfWork;
			_s3SettingsOptions = s3SettingsOptions.Value;
			_amazonS3 = amazonS3;
			_memoryCache = memoryCache;
			_logger = logger;
		}

		public async Task UpdateMeeting(ContributionBase contribution, Session session, SessionTime sessionTime, Cohere.Entity.Entities.User requesterUser)
        {
            using (var _zoomClient = await GetZoomClient(requesterUser.AccountId))
            {
                await _zoomClient.Meetings.UpdateScheduledMeetingAsync(sessionTime.ZoomMeetingData.MeetingId, null, session.Name,
                    null, DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, requesterUser.TimeZoneId), Convert.ToInt32((sessionTime.EndTime - sessionTime.StartTime).TotalMinutes), null, null,
                    new ZoomNet.Models.MeetingSettings
                    {
                        AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                    });
            }
        }

        public async Task ScheduleOrUpdateMeetings(ContributionBase contribution, Cohere.Entity.Entities.User requesterUser, SessionBasedContribution updatedCourse, SessionBasedContribution existedCourse)
        {
            using (var _zoomClient = await GetZoomClient(requesterUser.AccountId))
            {
                var zoomUser = await _zoomClient.Users.GetCurrentAsync(default(CancellationToken));
                foreach (var session in updatedCourse.Sessions)
                {
                    foreach (var sessionTime in session.SessionTimes)
                    {
                        var baseSessionTime = existedCourse.Sessions.FirstOrDefault(x => x.Id == session.Id)?.SessionTimes?.FirstOrDefault(x => x.Id == sessionTime.Id);
                        if (baseSessionTime == null)
                        {
                            var meeting = await _zoomClient.Meetings.CreateScheduledMeetingAsync(zoomUser.Id, session.Name, null, DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, requesterUser.TimeZoneId),
                                Convert.ToInt32((sessionTime.EndTime - sessionTime.StartTime).TotalMinutes), null, null,
                                new ZoomNet.Models.MeetingSettings
                                {
                                    AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                                });
                            sessionTime.ZoomMeetingData = new ZoomMeetingData
                            {
                                JoinUrl = meeting.JoinUrl,
                                StartUrl = meeting.StartUrl,
                                MeetingId = meeting.Id
                            };
                            contribution.ZoomMeetigsIds.Add(meeting.Id);
                        }
                        else if (baseSessionTime.StartTime != sessionTime.StartTime || baseSessionTime.EndTime != sessionTime.EndTime)
                        {
                            await _zoomClient.Meetings.UpdateScheduledMeetingAsync(sessionTime.ZoomMeetingData.MeetingId, null, session.Name,
                                null, DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, requesterUser.TimeZoneId), Convert.ToInt32((sessionTime.EndTime - sessionTime.StartTime).TotalMinutes), null, null,
                                new ZoomNet.Models.MeetingSettings
                                {
                                    AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                                });
                        }
                    }
                }
            }
        }

        public async Task<ScheduledMeeting> ScheduleMeeting(ContributionBase contribution, Session session, SessionTime sessionTime, Cohere.Entity.Entities.User requesterUser)
        {
            using (var _zoomClient = await GetZoomClient(requesterUser.AccountId))
            {
                var zoomUser = await _zoomClient.Users.GetCurrentAsync(default(CancellationToken));

                return await _zoomClient.Meetings.CreateScheduledMeetingAsync(zoomUser.Id, session.Name, null, DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, requesterUser.TimeZoneId),
                    Convert.ToInt32((sessionTime.EndTime - sessionTime.StartTime).TotalMinutes), null, null,
                    new ZoomNet.Models.MeetingSettings
                    {
                        AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                    });
            }
        }

        public async Task ScheduleMeetings(ContributionBase contribution, Cohere.Entity.Entities.User requesterUser, SessionBasedContribution updatedCourse)
        {
            using (var _zoomClient = await GetZoomClient(requesterUser.AccountId))
            {
                var zoomUser = await _zoomClient.Users.GetCurrentAsync(default(CancellationToken));
                foreach (var session in updatedCourse.Sessions)
                {
                    foreach (var sessionTime in session.SessionTimes)
                    {
                        var meeting = await _zoomClient.Meetings.CreateScheduledMeetingAsync(zoomUser.Id, session.Name, null, DateTimeHelper.GetZonedDateTimeFromUtc(sessionTime.StartTime, requesterUser.TimeZoneId),
                            Convert.ToInt32((sessionTime.EndTime - sessionTime.StartTime).TotalMinutes), null, null,
                            new ZoomNet.Models.MeetingSettings
                            {
                                AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                            });

                        if (sessionTime.ZoomMeetingData == null)
                        {
                            sessionTime.ZoomMeetingData = new ZoomMeetingData
                            {
                                MeetingId = meeting.Id,
                                JoinUrl = meeting.JoinUrl,
                                StartUrl = meeting.StartUrl
                            };
                        }
                        else
                        {
                            sessionTime.ZoomMeetingData.MeetingId = meeting.Id;
                            sessionTime.ZoomMeetingData.JoinUrl = meeting.JoinUrl;
                            sessionTime.ZoomMeetingData.StartUrl = meeting.StartUrl;
                        }
                        contribution.ZoomMeetigsIds.Add(meeting.Id);
                    }
                }
            }
        }


        //ScheduleMeeting for 1:1 contribution
        public async Task<ScheduledMeeting> ScheduleMeetingForOneToOne(string name, DateTime EndTime, DateTime StartTime, Cohere.Entity.Entities.User requesterUser)
        {
            using (var _zoomClient = await GetZoomClient(requesterUser.AccountId))
            {
                var zoomUser = await _zoomClient.Users.GetCurrentAsync(default(CancellationToken));

                return await _zoomClient.Meetings.CreateScheduledMeetingAsync(zoomUser.Id, name, null, DateTimeHelper.GetZonedDateTimeFromUtc(StartTime, requesterUser.TimeZoneId),
                    Convert.ToInt32((EndTime - StartTime).TotalMinutes), null, null,
                    new ZoomNet.Models.MeetingSettings
                    {
                        AutoRecording = ZoomNet.Models.RecordingType.OnCloud
                    });
            }
        }


        private async Task<ZoomClient> GetZoomClient(string accountId)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Id == accountId);

            var tokens = await GetTokens(account.ZoomRefreshToken, accountId);
            var connectionInfo = new OAuthConnectionInfo(_zoomOptions.ClientId, _zoomOptions.ClientSecret, tokens.refresh_token, tokens.access_token,
                                (newRefreshToken, newAccessToken) => { });
  
            account.ZoomRefreshToken = tokens.refresh_token;
            await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().Update(accountId, account);

            return new ZoomClient(connectionInfo);
        }

        public async Task DeleteMeeting(long meetingId, string accountId)
        {
            using (var _zoomClient = await GetZoomClient(accountId))
            {
                try
                {
                    await _zoomClient.Meetings.DeleteAsync(meetingId);
                }
                catch { }
            }
        }

        private async Task<Tokens> GetTokens(string refreshToken, string accountId)
        {
            return await _memoryCache.GetOrCreateAsync("zoomAuthToken_" + accountId, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(55));

                using (var client = new HttpClient())
                {
                    var plainTextBytes = System.Text.Encoding.UTF8.GetBytes($"{_zoomOptions.ClientId}:{_zoomOptions.ClientSecret}");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {System.Convert.ToBase64String(plainTextBytes)}");

                    var parameters = new Dictionary<string, string>();
                    var url = $"https://zoom.us/oauth/token?grant_type=refresh_token&refresh_token={refreshToken}";
                    var response = await client.PostAsync(url, new FormUrlEncodedContent(parameters));

                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Tokens>(responseString);
                }
            });
        }

        public async Task DisconnectZoom(string accountId)
        {
            await ClearAllUserZoomData(accountId);
            var tokens = _memoryCache.Get<Tokens>("zoomAuthToken_" + accountId);
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes($"{_zoomOptions.ClientId}:{_zoomOptions.ClientSecret}");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {System.Convert.ToBase64String(plainTextBytes)}");

                var parameters = new Dictionary<string, string>();
                var url = $"https://zoom.us/oauth/revoke?token={tokens.access_token}";
                var response = await client.PostAsync(url, new FormUrlEncodedContent(parameters));
            }
            _memoryCache.Remove("zoomAuthToken_" + accountId);
        }

        public string GetPresignedUrlForRecording(long meetingId, string fileName, bool asAttachment)
        {
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _s3SettingsOptions.NonPublicBucketName,
                Key = $"{meetingId}/{fileName}",
                Expires = asAttachment ? DateTime.UtcNow.AddMinutes(5) : DateTime.UtcNow.AddDays(1)
            };
            if (asAttachment)
            {
                request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename={fileName}";
                request.ResponseHeaderOverrides.ContentType = "application/octet-stream";
            }

            return _amazonS3.GetPreSignedURL(request);
        }

        public async Task SaveZoomRefreshToken(string authCode, string accountId, string redirectUri)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes($"{_zoomOptions.ClientId}:{_zoomOptions.ClientSecret}");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Basic {System.Convert.ToBase64String(plainTextBytes)}");

                var parameters = new Dictionary<string, string>();
                parameters["grant_type"] = authCode;
                parameters["code"] = "code";
                parameters["redirect_uri"] = redirectUri;

                var url = $"https://zoom.us/oauth/token?grant_type=authorization_code&code={authCode}";
                var response = await client.PostAsync(url, new FormUrlEncodedContent(parameters));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var tokens = JsonConvert.DeserializeObject<Tokens>(responseString);
                    _memoryCache.Set("zoomAuthToken_" + accountId, tokens, TimeSpan.FromMinutes(55));

                    var connectionInfo = new OAuthConnectionInfo(_zoomOptions.ClientId, _zoomOptions.ClientSecret, tokens.refresh_token, tokens.access_token,
                                    (newRefreshToken, newAccessToken) => { });

                    using (var zoomClient = new ZoomClient(connectionInfo))
                    {
                        var zoomUser = await zoomClient.Users.GetCurrentAsync(default(CancellationToken));

                        var account = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Id == accountId);
                        account.ZoomRefreshToken = tokens.refresh_token;
                        account.ZoomUserId = zoomUser.Id;

                        await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().Update(accountId, account);
                    }
                }
            }
        }

        public async Task DeauthorizeUser(string user_id)
        {
            var account = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.ZoomUserId == user_id);
            await ClearAllUserZoomData(account.Id);
            _memoryCache.Remove("zoomAuthToken_" + account.Id);
        }

        private async Task ClearAllUserZoomData(string accountId)
		{
            try
            {
                using (var _zoomClient = await GetZoomClient(accountId))
                {
                    var user = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.User>().GetOne(x => x.AccountId == accountId);
                    var contributions = await _unitOfWork.GetRepositoryAsync<ContributionBase>().Get(u => u.UserId == user.Id && u.LiveVideoServiceProvider.ProviderName == Constants.LiveVideoProviders.Zoom);

                    foreach (var contribution in contributions.OfType<SessionBasedContribution>())
                    {
                        contribution.LiveVideoServiceProvider.ProviderName = Constants.LiveVideoProviders.Cohere;
                        contribution.ZoomMeetigsIds = new List<long>();
                        foreach (var session in contribution.Sessions)
                        {
                            foreach (var sessionTime in session.SessionTimes)
                            {
                                if (sessionTime.ZoomMeetingData != null)
                                {
                                    sessionTime.ZoomMeetingData.JoinUrl = null;
                                    sessionTime.ZoomMeetingData.StartUrl = null;
                                    if (sessionTime.StartTime > DateTime.UtcNow)
                                    {
                                        try
                                        {
                                            await _zoomClient.Meetings.DeleteAsync(sessionTime.ZoomMeetingData.MeetingId);
                                        }
                                        catch { }
                                    }
                                    if (!sessionTime.ZoomMeetingData.ChatFiles.Any())
                                    {
                                        sessionTime.ZoomMeetingData.MeetingId = 0;
                                    }
                                }
                            }
                        }
                        await _unitOfWork.GetRepositoryAsync<ContributionBase>().Update(contribution.Id, contribution);
                    }
                    EmptyZoomInfoFromAccountAsync(accountId);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Value cannot be null. (Parameter 'refreshToken')") 
                {
                    EmptyZoomInfoFromAccountAsync(accountId);
                }
            }
        }

        public async void EmptyZoomInfoFromAccountAsync(string accountId) 
        {
            var account = await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().GetOne(a => a.Id == accountId);

            account.ZoomRefreshToken = null;
            account.ZoomUserId = null;
            await _unitOfWork.GetRepositoryAsync<Cohere.Entity.Entities.Account>().Update(accountId, account);
        }
    }
}
