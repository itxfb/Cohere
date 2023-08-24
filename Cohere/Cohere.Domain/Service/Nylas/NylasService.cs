using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cohere.Domain.Infrastructure;
using Cohere.Domain.Infrastructure.Generic;
using Cohere.Domain.Models.User;
using Cohere.Domain.Service.Abstractions;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;
using Cohere.Entity.EntitiesAuxiliary.Contribution;
using Cohere.Entity.Repository.Abstractions.Generic;
using Cohere.Entity.UnitOfWork;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;

namespace Cohere.Domain.Service.Nylas
{
    public class NylasService
    {
        public const string ClientId = "NylasClientId";
        public const string ClientSecret = "NylasClientSecret";
        public const string AuthorisationUrlPath = "AuthorisationUrlPath";
        public const string TokenUrlPath = "TokenUrlPath";
        public const string FreeBusyUrlPath = "FreeBusyUrlPath";
        public const string RedirectUri = "RedirectUri";
        public const string Scopes = "Scopes";
        public const string ResponseType = "ResponseType";
        public const string GrantType = "GrantType";

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _authorisationUrlPath;
        private readonly string _tokenUrlPath;
        private readonly string _freeBusyUrlPath;
        private readonly string _redirectUri;
        private readonly string _scopes;
        private readonly string _grantType;
        private readonly string _responseType;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;
        private readonly IRepositoryAsync<NylasAccount> _nylasAccountRepository;

        public NylasService(Func<string, string> settingsResolver, IUnitOfWork unitOfWork, IMemoryCache memoryCache)
        {
            _clientId = settingsResolver.Invoke(ClientId);
            _clientSecret = settingsResolver.Invoke(ClientSecret);
            _authorisationUrlPath = settingsResolver.Invoke(AuthorisationUrlPath);
            _tokenUrlPath = settingsResolver.Invoke(TokenUrlPath);
            _freeBusyUrlPath = settingsResolver.Invoke(FreeBusyUrlPath);
            _redirectUri = settingsResolver.Invoke(RedirectUri);
            _scopes = settingsResolver.Invoke(Scopes);
            _grantType = settingsResolver.Invoke(GrantType);
            _responseType = settingsResolver.Invoke(ResponseType);

            _unitOfWork = unitOfWork;
            _memoryCache = memoryCache;
            _nylasAccountRepository = _unitOfWork.GetRepositoryAsync<NylasAccount>();
        }

        public OperationResult GetAuthorisationUrl(string contributionId, bool isCreate)
        {
            if (string.IsNullOrEmpty(_authorisationUrlPath) ||
                string.IsNullOrEmpty(_clientId) ||
                string.IsNullOrEmpty(_responseType) ||
                string.IsNullOrEmpty(_scopes) ||
                string.IsNullOrEmpty(_redirectUri))
            {
                return OperationResult.Failure(string.Empty);
            }

            var basePath = $"{_authorisationUrlPath}?client_id={_clientId}&response_type={_responseType}&scopes={_scopes}&redirect_uri={_redirectUri}";
            if (isCreate)
            {
                return OperationResult.Success(
                    string.Empty,
                    $"{basePath}&state=create");
            }

            if (!string.IsNullOrWhiteSpace(contributionId))
            {
                return OperationResult.Success(
                    string.Empty,
                    $"{basePath}&state={contributionId}");
            }

            return OperationResult.Success(string.Empty, basePath);
        }

        public async Task<OperationResult> AddAccountAsync(string accountId, string code)
        {
            if (string.IsNullOrEmpty(accountId) ||
                string.IsNullOrEmpty(code))
            {
                return OperationResult.Failure(string.Empty);
            }

            if (string.IsNullOrEmpty(_tokenUrlPath) ||
                string.IsNullOrEmpty(_clientId) ||
                string.IsNullOrEmpty(_clientSecret) ||
                string.IsNullOrEmpty(_grantType))
            {
                return OperationResult.Failure(string.Empty);
            }

            var client = new RestClient(_tokenUrlPath);

            var request = new RestRequest(Method.POST);
            request.AddQueryParameter("client_id", _clientId);
            request.AddQueryParameter("client_secret", _clientSecret);
            request.AddQueryParameter("grant_type", _grantType);
            request.AddQueryParameter("code", code);

            var response = await client.ExecuteAsync<TokenResponse>(request);

            if (response.Data == null)
            {
                return OperationResult.Failure(string.Empty);
            }

            var nylasAccount = new NylasAccount()
            {
                CohereAccountId = accountId,
                NylasAccountId = response.Data.account_id,
                AccessToken = response.Data.access_token,
                EmailAddress = response.Data.email_address,
                TokenType = response.Data.token_type,
                Provider = response.Data.provider,
                IsCheckConflictsEnabled = false,
                IsEventRemindersEnabled = false
            };
            //Get Primary CalendarId
            CalendarResponse calendarResponse = await GetPrimaryCalendarId(nylasAccount);
            if (calendarResponse != null && !string.IsNullOrEmpty(calendarResponse.id))
                nylasAccount.CalendarId = calendarResponse.id;

            return await AddOrUpdateNylasAccountAsync(nylasAccount);
        }
         public async Task<OperationResult> DeleteAccountFromNylas(NylasAccount account)
        {
            if (account==null)
            {
                return OperationResult.Failure(string.Empty);
            }

            if (string.IsNullOrEmpty(_tokenUrlPath) ||
                string.IsNullOrEmpty(_clientId) ||
                string.IsNullOrEmpty(_clientSecret) ||
                string.IsNullOrEmpty(_grantType))
            {
                return OperationResult.Failure(string.Empty);
            }

            var client = new RestClient($"https://api.nylas.com/a/{_clientId}/accounts/{account.NylasAccountId}");
            client.Timeout = -1;
            var request = new RestRequest(Method.DELETE);
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(_clientSecret+":");
            string val = System.Convert.ToBase64String(plainTextBytes);
            request.AddHeader("Authorization", "Basic "+val);
            request.AddHeader("Cookie", "__cf_bm=DDqOjx3WMqnVW4UL.gP2Y5uyM.6ZHI8KMJMWDrnc2X0-1675680157-0-AdSuNx6TnMvoCf37LJ/CWOAUbxk8/Z/BKXeXuEWyDFy8J7ruFlXtdfhXz3t8JoSS4o0GehMPFahKpgP1Yk/zOwQ=");
            IRestResponse response = await client.ExecuteAsync(request);
            Console.WriteLine(response.Content);

            

                return OperationResult.Success(string.Empty);

        }

        public async Task<CalendarResponse> GetPrimaryCalendarId(NylasAccount nylasAccount)
        {
            RestClient obj = new RestClient();
            RestRequest rest = new RestRequest()
            {
                Resource = "https://api.nylas.com/calendars"
            };
            rest.AddHeader("Content-Type", "application/json");
            rest.AddHeader("Accept", "application/xml");
            rest.AddHeader("Authorization", "Bearer "+ nylasAccount.AccessToken);
            var response = await obj.ExecuteAsync<List<CalendarResponse>>(rest);
            if(response.Data == null)
            {
                return new CalendarResponse();

            }

            var primarycalendar = response.Data.Where(x=>x.is_primary==true);

            if (!primarycalendar.Any())
            {
                primarycalendar = response.Data.Where(x => x.read_only == false);
                if (!primarycalendar.Any())
                {
                    return new CalendarResponse();

                }
                else
                {
                    return primarycalendar.FirstOrDefault();

                }
            }
            else
            {
                return primarycalendar.FirstOrDefault();

            }
        }
        private async Task<OperationResult> AddOrUpdateNylasAccountAsync(NylasAccount nylasAccount)
        {
            if (nylasAccount == null ||
               string.IsNullOrEmpty(nylasAccount.CohereAccountId) ||
               string.IsNullOrEmpty(nylasAccount.NylasAccountId) ||
               string.IsNullOrEmpty(nylasAccount.EmailAddress))
            {
                return OperationResult.Failure(string.Empty);
            }
            //Check if its first calendar of this account of not
            var firstAccount = await _nylasAccountRepository
            .GetOne(a => a.CohereAccountId == nylasAccount.CohereAccountId);
            
            var account = await _nylasAccountRepository
            .GetOne(a => a.CohereAccountId == nylasAccount.CohereAccountId &&
            a.EmailAddress == nylasAccount.EmailAddress);

           

            if (account == null)
            {
                if (firstAccount == null)
                {
                    nylasAccount.IsDefault = true;
                }
                else
                {
                    nylasAccount.IsDefault = false;
                }

                await _nylasAccountRepository.Insert(nylasAccount);
            }
            else
            {
                await _nylasAccountRepository.Update(account.Id, nylasAccount);
            }

            return OperationResult.Success(string.Empty, nylasAccount.EmailAddress);
        }

        public async Task<OperationResult> RemoveNylasAccountAsync(string accountId, string emailAddress)
        {
            if (string.IsNullOrEmpty(accountId) ||
                string.IsNullOrEmpty(emailAddress))
            {
                return OperationResult.Failure(string.Empty);
            }
            var account = await _nylasAccountRepository
                .GetOne(a => a.CohereAccountId == accountId &&
                    a.EmailAddress == emailAddress);

            if (account == null)
            {
                return OperationResult.Failure(string.Empty);
            }

            await _nylasAccountRepository.Delete(account);
            await DeleteAccountFromNylas(account);

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> RemoveNylasAccountForInActiveUsersAsync()
        {
            try
            {
            var lastDate = System.DateTime.UtcNow.AddDays(-120);
            var InActiveUsers = await _unitOfWork.GetRepositoryAsync<UserActivity>().GetAll();
            var GroupedUsers = InActiveUsers.GroupBy(r => r.UserId).Select(g => g.OrderByDescending(x => x.ActivityTimeUTC).First()).Where(x=>x.ActivityTimeUTC<=lastDate).Select(u=>u.UserId).ToList();
            var users = await _unitOfWork.GetRepositoryAsync<User>().Get(u => GroupedUsers.Contains(u.Id));
            var AccountIds = new List<string>();
            
            foreach (var AccountId in users.Select(x=>x.AccountId))
            {
                var getNylasAccounts =await _unitOfWork.GetRepositoryAsync<NylasAccount>().Get(x => x.CohereAccountId == AccountId);
                foreach (var account in getNylasAccounts)
                {
                    await _nylasAccountRepository.Delete(account);
                    await DeleteAccountFromNylas(account);

                    }
                }
            return OperationResult.Success("Nylas Accounts Removed Successfully");
            }
            catch
            {
                return OperationResult.Success("Nylas Accounts Removed Successfully");
            }
        }
        public async Task<OperationResult> DefaultsNylasAccountAsync(string accountId, string emailAddress)
        {
            if (string.IsNullOrEmpty(accountId) ||
                string.IsNullOrEmpty(emailAddress))
            {
                return OperationResult.Failure(string.Empty);
            }
            List<NylasAccount> defaultaccounts =  _nylasAccountRepository.GetAll().Result.Where(a => a.IsDefault == true && a.CohereAccountId == accountId).ToList();
            if (defaultaccounts.Any())
            {
                foreach(var nylasaccount in defaultaccounts)
                {
                    nylasaccount.IsDefault = false;
                   await _nylasAccountRepository.Update(nylasaccount.Id, nylasaccount);
                }
            }

            var account = await _nylasAccountRepository
                .GetOne(a => a.EmailAddress == emailAddress &&
                    a.CohereAccountId == accountId);

            if (account == null)
            {
                return OperationResult.Failure(string.Empty);
            }
            account.IsDefault = true;

            await _nylasAccountRepository.Update(account.Id,account);
            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> EnableCheckCalendarConflictsForNylasAccountsAsync(string accountId, IEnumerable<string> emailAddresses)
        {
            if (string.IsNullOrEmpty(accountId) ||
                emailAddresses == null ||
                emailAddresses.Count() == 0)
            {
                return OperationResult.Failure(string.Empty);
            }

            var accounts = await _nylasAccountRepository
            .Get(a => a.CohereAccountId == accountId);

            if (accounts == null || accounts.Count() == 0)
            {
                return OperationResult.Failure(string.Empty);
            }

            foreach (NylasAccount account in accounts)
            {
                if (emailAddresses.Contains(account.EmailAddress) && account.IsCheckConflictsEnabled != true)
                {
                    account.IsCheckConflictsEnabled = true;
                    await _nylasAccountRepository.Update(account.Id, account);
                }
                else if (!account.IsCheckConflictsEnabled)
                {
                    account.IsCheckConflictsEnabled = false;
                    await _nylasAccountRepository.Update(account.Id, account);
                }
            }

            return OperationResult.Success(string.Empty);
        }
        public async Task<OperationResult> DisableCheckCalendarConflictsForNylasAccountsAsync(string accountId, string emailAddress)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(emailAddress))
            {
                return OperationResult.Failure(string.Empty);
            }

            var account = await _nylasAccountRepository
            .GetOne(a => a.CohereAccountId == accountId && a.EmailAddress.ToLower()==emailAddress.ToLower());

            if (account == null)
            {
                return OperationResult.Failure(string.Empty);
            }

            if (account.IsCheckConflictsEnabled == true)
            {
                account.IsCheckConflictsEnabled = false;
                await _nylasAccountRepository.Update(account.Id, account);
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> EnableEventRemindersForNylasAccountAsync(string accountId, string emailAddress)
        {
            if (string.IsNullOrEmpty(accountId) ||
                string.IsNullOrEmpty(emailAddress))
            {
                return OperationResult.Failure(string.Empty);
            }

            var accounts = await _nylasAccountRepository
            .Get(a => a.CohereAccountId == accountId);

            if (accounts == null || accounts.Count() == 0)
            {
                return OperationResult.Failure(string.Empty);
            }

            foreach (NylasAccount account in accounts)
            {
                if (account.EmailAddress == emailAddress && account.IsEventRemindersEnabled != true)
                {
                    account.IsEventRemindersEnabled = true;
                    await _nylasAccountRepository.Update(account.Id, account);
                }
                else if (!account.IsEventRemindersEnabled)
                {
                    account.IsEventRemindersEnabled = false;
                    await _nylasAccountRepository.Update(account.Id, account);
                }
            }

            return OperationResult.Success(string.Empty);
        }

        public async Task<OperationResult> GetNylasAccountsForCohereAccountAsync(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return OperationResult.Failure(string.Empty);
            }

            var accounts = await _nylasAccountRepository
            .Get(a => a.CohereAccountId == accountId);

            if (accounts == null || accounts.Count() == 0)
            {
                return OperationResult.Success(string.Empty);
            }

            var result = accounts.Select(x => new ExternalCalendarAccountViewModel
            {
                EmailAddress = x.EmailAddress,
                Provider = x.Provider,
                IsCheckConflictsEnabled = x.IsCheckConflictsEnabled,
                IsEventRemindersEnabled = x.IsEventRemindersEnabled,
                IsDefault = x.IsDefault
            });

            return OperationResult.Success(string.Empty, result);
        }

        public async Task<OperationResult> GetNylasAccountsWithCheckConflictsEnabledForCohereAccountAsync(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return OperationResult.Failure(string.Empty);
            }

            var accounts = await _nylasAccountRepository
            .Get(a => a.CohereAccountId == accountId && a.IsCheckConflictsEnabled);

            if (accounts == null || accounts.Count() == 0)
            {
                return OperationResult.Success("Calendars not connected");
            }

            var result = accounts.Select(x => new ExternalCalendarAccountViewModel
            {
                EmailAddress = x.EmailAddress,
                Provider = x.Provider,
                IsCheckConflictsEnabled = x.IsCheckConflictsEnabled,
                IsEventRemindersEnabled = x.IsEventRemindersEnabled,
                IsDefault = x.IsDefault
                
            });

            return OperationResult.Success(string.Empty, result);
        }

        public async Task<OperationResult> GetNylasAccountWithEventRemindersEnabledForCohereAccountAsync(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return OperationResult.Success(string.Empty);
            }

            var account = await _nylasAccountRepository
            .GetOne(a => a.CohereAccountId == accountId && a.IsEventRemindersEnabled);

            if (account == null)
            {
                return OperationResult.Success("Calendars not connected");
            }

            var result = new ExternalCalendarAccountViewModel
            {
                EmailAddress = account.EmailAddress,
                Provider = account.Provider,
                IsCheckConflictsEnabled = account.IsCheckConflictsEnabled,
                IsEventRemindersEnabled = account.IsEventRemindersEnabled,
                IsDefault =account.IsDefault
            };

            return OperationResult.Success(string.Empty, result);
        }

        public async Task<OperationResult<List<TimeRange>>> GetBusyTimesInUtc(string accountId, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
        {
            var busyTimes = await GetBusyTimeForAccount(
                accountId,
                startTime,
                endTime);

            if (busyTimes.Failed)
            {
                return busyTimes;
            }

            var cohealerBusyTimesFromCalendars = busyTimes.Payload;

            return new OperationResult<List<TimeRange>>(cohealerBusyTimesFromCalendars);
        }

        public async Task<OperationResult<List<TimeRange>>> GetBusyTimes(string accountId, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
        {
            var contributor = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);
            var timeZoneId = contributor.TimeZoneId;

            var busyTimesInUtc = await GetBusyTimesInUtc(
                accountId,
                startTime.HasValue ? new DateTimeOffset(DateTimeHelper.GetUtcTimeFromZoned(startTime.Value.LocalDateTime, timeZoneId)) : default(DateTimeOffset?),
                endTime.HasValue ? new DateTimeOffset(DateTimeHelper.GetUtcTimeFromZoned(endTime.Value.LocalDateTime, timeZoneId)) : default(DateTimeOffset?));

            if (busyTimesInUtc.Failed)
            {
                return busyTimesInUtc;
            }

            return new OperationResult<List<TimeRange>>(
                busyTimesInUtc.Payload
                .Select(e => new TimeRange()
                {
                    StartTime = DateTimeHelper.GetZonedDateTimeFromUtc(e.StartTime.ToUniversalTime(), timeZoneId),
                    EndTime = DateTimeHelper.GetZonedDateTimeFromUtc(e.EndTime.ToUniversalTime(), timeZoneId)
                })
                .ToList());
        }

        private async Task<OperationResult<List<TimeRange>>> GetBusyTimeForAccount(string accountId, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return OperationResult<List<TimeRange>>.Failure("accountId is missing");
            }

            var accounts = await _nylasAccountRepository
            .Get(a => a.CohereAccountId == accountId && a.IsCheckConflictsEnabled);

            if (accounts == null || accounts.Count() == 0)
            {
                return OperationResult<List<TimeRange>>.Failure("calendar not selected");
            }

            var cohealerBusyTimesFromCalendars = new List<TimeRange>();

            foreach (NylasAccount account in accounts)
            {
                var busyTimesResult = await GetCohealerBusyTimesFromCalendarsAsync(
                        account.AccessToken, new List<string>
                        {
                                account.EmailAddress
                        },
                        startTime,
                        endTime);

                var busyTimes = (IEnumerable<TimeRange>)busyTimesResult.Payload;
                if (busyTimes != null)
                {
                    cohealerBusyTimesFromCalendars.AddRange(busyTimes);
                }
            }

            return new OperationResult<List<TimeRange>>(cohealerBusyTimesFromCalendars);
        }

        private async Task<OperationResult<List<NylasFreeBusy>>> GetBusyTime(
            string accessToken,
            IEnumerable<string> emailAddresses,
            DateTimeOffset? startTime = default,
            DateTimeOffset? endTime = default)
        {
            if (string.IsNullOrEmpty(accessToken) ||
                emailAddresses == null ||
                emailAddresses.Count() == 0)
            {
                return OperationResult<List<NylasFreeBusy>>.Failure("Access Token not defined");
            }

            if (string.IsNullOrEmpty(_freeBusyUrlPath))
            {
                return OperationResult<List<NylasFreeBusy>>.Failure("Free Busy Url Path not defined");
            }

            List<NylasFreeBusy> busyTimes = await GetBusyTimeForLongTimeRange(accessToken, emailAddresses, startTime, endTime);

            return new OperationResult<List<NylasFreeBusy>>(busyTimes ?? new List<NylasFreeBusy>());
        }

        private async Task<List<NylasFreeBusy>> GetBusyTimeForLongTimeRange(string accessToken, IEnumerable<string> emailAddresses, DateTimeOffset? startTime, DateTimeOffset? endTime)
        {
            var requestStartTime = new DateTimeOffset(startTime.GetValueOrDefault(DateTimeOffset.UtcNow).Date); //start from beginning of day to reduce cache missing
            var requestEndTime = new DateTimeOffset(endTime.GetValueOrDefault(DateTimeOffset.UtcNow.AddDays(90)).Date.AddDays(1)); //end at beginning of next day to reduce cache missing

            var result = new List<NylasFreeBusy>();

            var maxTimeRange = 90;

            for (var splitStartTime = requestStartTime; splitStartTime < requestEndTime; splitStartTime = splitStartTime + TimeSpan.FromDays(maxTimeRange))
            {
                var splitEndTime = splitStartTime + TimeSpan.FromDays(maxTimeRange);
                if (splitEndTime > requestEndTime)
                {
                    splitEndTime = requestEndTime;
                }

                var enumerable = emailAddresses as string[] ?? emailAddresses.ToArray();
                var nylasFreeBusyRequest = new NylasFreeBusyRequest()
                {
                    start_time = splitStartTime.ToUnixTimeSeconds().ToString(),
                    end_time = splitEndTime.ToUnixTimeSeconds().ToString(),
                    emails = enumerable
                };

                //var busyTimes = await _memoryCache.GetOrCreateAsync(nylasFreeBusyRequest, async entry =>
                //{
                //    entry.SetSlidingExpiration(TimeSpan.FromHours(1));
                //    return await NylasFreeBusyTimeRequest(accessToken, entry.Key as NylasFreeBusyRequest);
                //});
                var busyTimes = await NylasFreeBusyTimeRequest(accessToken, nylasFreeBusyRequest);

                if (busyTimes != null)
                {
                    result.AddRange(busyTimes);
                }
            }

            return result;
        }

        private async Task<List<NylasFreeBusy>> NylasFreeBusyTimeRequest(string accessToken, NylasFreeBusyRequest nylasRequest)
        {
            var client = new RestClient(_freeBusyUrlPath);

            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddParameter("application/json", JsonSerializer.Serialize(nylasRequest), ParameterType.RequestBody);

            var response = await client.ExecuteAsync<IEnumerable<NylasFreeBusy>>(request);

            return response?.Data?.ToList();
        }

        private async Task<OperationResult> GetCohealerBusyTimesFromCalendarsAsync(
            string accessToken,
            IEnumerable<string> emailAddresses,
            DateTimeOffset? startTime = default,
            DateTimeOffset? endTime = default)
        {
            var response = await GetBusyTime(accessToken, emailAddresses, startTime, endTime);

            if (!response.Succeeded)
            {
                return OperationResult.Failure("Can't obtain data from Nylas");
            }

            if (response == null || response.Payload.Count == 0)
            {
                return OperationResult.Failure("Empty Nylas Response");
            }

            var freeBusyes = response.Payload;

            var cohealerBusyTimes = new List<TimeRange>();

            foreach (NylasFreeBusy fb in freeBusyes)
            {
                cohealerBusyTimes.AddRange(fb.TimeRanges);
            }

            return OperationResult.Success(string.Empty, cohealerBusyTimes);
        }

        public async Task InvalidateCache(IEnumerable<NylasEvent> model)
        {
            var nylasAccountIds = model.Select(e => e.Data.AccountId).Distinct();

            var updatedAccounts = await _nylasAccountRepository.Get(e => nylasAccountIds.Contains(e.NylasAccountId));

            var allKeys = _memoryCache.GetKeys<NylasFreeBusyRequest>();

            foreach (var account in updatedAccounts)
            {
                var keysToInvalidate = allKeys.Where(e => e.emails.Contains(account.EmailAddress)).ToList();

                keysToInvalidate.ForEach(e => _memoryCache.Remove(e));
            }
        }
    }
}
