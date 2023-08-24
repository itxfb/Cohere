using System.Collections.Generic;
using System.Linq;
using Cohere.Api.Settings;
using Cohere.Entity.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Security;

namespace Cohere.Api.Filters
{
    public class ValidateTwilioRequestAttribute : ActionFilterAttribute
    {
        private readonly RequestValidator _requestValidator;
        private readonly ILogger<ValidateTwilioRequestAttribute> _logger;

        public ValidateTwilioRequestAttribute(IOptions<SecretsSettings> options, ILogger<ValidateTwilioRequestAttribute> logger)
        {
            _requestValidator = new RequestValidator(options.Value.TwilioAccountAuthToken);
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext actionContext)
        {
            var context = actionContext.HttpContext;
            if (!IsValidRequest(context.Request))
            {
                actionContext.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            }

            base.OnActionExecuting(actionContext);
        }

        private static string RequestRawUrl(HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        private static IDictionary<string, string> ToDictionary(IFormCollection collection)
        {
            return collection.Keys
                .Select(key => new { Key = key, Value = collection[key] })
                .ToDictionary(p => p.Key, p => p.Value.ToString());
        }

        private bool IsValidRequest(HttpRequest request)
        {
            var requestUrl = RequestRawUrl(request);
            if(!requestUrl.Contains("https") && !requestUrl.Contains("localhost"))
            {
                requestUrl =  System.Text.RegularExpressions.Regex.Replace(requestUrl, @"http", "https");
            }
            var parameters = ToDictionary(request.Form);
            var signature = request.Headers["X-Twilio-Signature"];
            var isValid = _requestValidator.Validate(requestUrl, parameters, signature);

            _logger.Log(LogLevel.Information, $"TwilioValidation request valid: {isValid} to URL: {requestUrl}");
            return isValid;
        }
    }
}
