using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Cohere.Domain.Models;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.TimeZone;
using Cohere.Domain.Service.Abstractions.Generic;
using Cohere.Domain.Utils;
using Cohere.Entity.Entities;

using FluentValidation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Cohere.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ReferenceDataController : ControllerBase
    {
        private readonly IServiceAsync<PreferenceViewModel, Preference> _preferencesService;
        private readonly IServiceAsync<SecurityQuestionViewModel, SecurityQuestion> _securityQuestionService;
        private readonly IServiceAsync<AgreementViewModel, Agreement> _agreementService;
        private readonly IServiceAsync<TimeZoneViewModel, TimeZone> _timeZoneService;
        private readonly IServiceAsync<CountryViewModel, Country> _countryService;
        private readonly IValidator<LocationViewModel> _locationValidator;
        private readonly IValidator<TimeZoneViewModel> _timeZoneValidator;
        private readonly IValidator<CountryViewModel> _countryValidator;
        private readonly ILogger<ReferenceDataController> _logger;

        public ReferenceDataController(
            IServiceAsync<PreferenceViewModel, Preference> preferencesService,
            IServiceAsync<SecurityQuestionViewModel, SecurityQuestion> securityQuestionService,
            IServiceAsync<AgreementViewModel, Agreement> agreementService,
            IServiceAsync<TimeZoneViewModel, TimeZone> timeZoneService,
            IServiceAsync<CountryViewModel, Country> countryService,
            IValidator<LocationViewModel> locationValidator,
            IValidator<TimeZoneViewModel> timeZoneValidator,
            IValidator<CountryViewModel> countryValidator,
            ILogger<ReferenceDataController> logger)
        {
            _preferencesService = preferencesService;
            _securityQuestionService = securityQuestionService;
            _locationValidator = locationValidator;
            _timeZoneValidator = timeZoneValidator;
            _countryValidator = countryValidator;
            _agreementService = agreementService;
            _timeZoneService = timeZoneService;
            _countryService = countryService;
            _logger = logger;
        }

        // GET: ReferenceData/Preferences
        [Authorize]
        [HttpGet("Preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var preferences = await _preferencesService.GetAll();
            return Ok(preferences);
        }

        // GET: ReferenceData/SecurityQuestions
        [Authorize]
        [HttpGet("SecurityQuestions")]
        public async Task<IActionResult> GetSecurityQuestions()
        {
            var securityQuestions = await _securityQuestionService.GetAll();
            return Ok(securityQuestions);
        }

        // GET: ReferenceData/GetAgreements
        [HttpGet("GetAgreements")]
        public async Task<IActionResult> GetAgreements()
        {
            var agreements = await _agreementService.Get(a => a.IsLatest);
            if (agreements.Any())
            {
                return Ok(agreements);
            }

            return NotFound();
        }

        // POST: ReferenceData/GetTimeZoneName
        [HttpPost("GetTimeZoneName")]
        public IActionResult GetTimeZoneName([FromBody] LocationViewModel location)
        {
            var validationResult = _locationValidator.Validate(location);

            if (validationResult.IsValid)
            {
                var timeZoneIanaId =
                    DateTimeHelper.CalculateTimeZoneIanaId(location.Latitude, location.Longitude);
                if (!string.IsNullOrEmpty(timeZoneIanaId))
                {
                    return Ok(timeZoneIanaId);
                }

                return BadRequest(new ErrorInfo { Message = "Unable get time zone name by coordinates provided" });
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // GET: ReferenceData/GetTimeZones
        [AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        [HttpGet("GetTimeZones")]
        public async Task<IActionResult> GetTimeZones()
		{
            var timeZones = await this._timeZoneService.GetAll();
            var favoriteTimeZones = timeZones.Where(a => a.IsFavourite);
            timeZones = timeZones.Where(a => !a.IsFavourite);
            timeZones = timeZones.OrderBy(x => x.Name);

            timeZones = favoriteTimeZones.Concat(timeZones);
            return Ok(timeZones);
        }

		// POST: ReferenceData/TimeZone
		[Authorize(Roles = "SuperAdmin")]
		[HttpPost("TimeZone")]
        public async Task<IActionResult> AddTimeZone([FromBody] TimeZoneViewModel timeZone)
        {
            var validationResult = await _timeZoneValidator.ValidateAsync(timeZone);

            if (validationResult.IsValid)
            {
                try
                {
                    var result = await _timeZoneService.Insert(timeZone);
                    var timeZoneInserted = (TimeZoneViewModel)result.Payload;
                    if (result.Succeeded)
                    {
                        return Created($"TimeZone/{timeZoneInserted.Id}", timeZoneInserted); //HTTP201 Resource created
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError($"Exception occured during time zone insertion: {ex.Message}");
                    throw;
                }
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // GET: ReferenceData/GetCountries
        [AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        [HttpGet("GetCountries")]
        public async Task<IActionResult> GetCountries()
        {
            var countries = await this._countryService.GetAll();

            return Ok(countries.OrderBy(a => a.Name));
        }

		// POST: ReferenceData/Country
		[Authorize(Roles = "SuperAdmin")]
		[HttpPost("Country")]
        public async Task<IActionResult> AddCountry([FromBody] CountryViewModel country)
        {
            var validationResult = await _countryValidator.ValidateAsync(country);

            if (validationResult.IsValid)
            {
                try
                {
                    var result = await _countryService.Insert(country);
                    var countryInserted = (CountryViewModel)result.Payload;
                    if (result.Succeeded)
                    {
                        return Created($"Country/{countryInserted.Id}", countryInserted); //HTTP201 Resource created
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError($"Exception occured during time zone insertion: {ex.Message}");
                    throw;
                }
            }

            return BadRequest(new ErrorInfo { Message = validationResult.ToString() });
        }

        // POST: ReferenceData/Preference
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("Preference")]
        public async Task<IActionResult> AddPreference(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest(new ErrorInfo("Preference is empty"));
            }

            var preference = new PreferenceViewModel()
            {
                Name = name
            };

            var result = await _preferencesService.Insert(preference);
            var preferenceInserted = (PreferenceViewModel)result.Payload;
            if (result.Succeeded)
            {
                return Created($"Preference/{preferenceInserted.Id}", preferenceInserted); //HTTP201 Resource created
            }

            return BadRequest(new ErrorInfo("Unable to create preference"));
        }
    }
}
