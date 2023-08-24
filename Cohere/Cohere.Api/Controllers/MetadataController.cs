using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cohere.Domain.Models;
using Cohere.Domain.Service.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Routing.Template;
using Org.BouncyCastle.Asn1.Cms;
using Microsoft.Extensions.Logging;

namespace Cohere.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MetadataController : ControllerBase
    {
        private readonly IContributionService _contributionService;
        private readonly ILogger<IContributionService> _logger;

        public MetadataController(IContributionService contributionService, ILogger<IContributionService> logger)
        {
            _contributionService = contributionService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Index([FromBody]GetMetadataRequest request)
        {
            _logger.LogError($"MetaData - request url is: {request.Url}");
            ContributionMetadataViewModel metadata = null;
            string url = request.Url.ToLower();
            int length = url.IndexOf(@".cohere.live");
            if (length > 0) {
                string prefix = url.Substring(0, length);
                prefix = Regex.Replace(prefix, @"^https?://", "");
                prefix = Regex.Replace(prefix, @"\.(test|dev|aqa)$", "");
                if (string.IsNullOrWhiteSpace(prefix) == false)
                {
                    metadata = await _contributionService.GetWebsiteLinkMetadata(prefix);
                }
            }
            else 
            {
                metadata = null;
            }
            if (metadata == null)
            {
                var result = RouteMatcher.Match("/{controller}/{contributionId}/{*page}", request.Url);


                if (result.ContainsKey("controller") && result.ContainsKey("contributionId"))
                {
                    var controller = result["controller"] as string;
                    var contributionId = result["contributionId"] as string;
                    if (controller == "contribution-view")
                    {
                        metadata = await _contributionService.GetContributionMetadata(contributionId) ?? new ContributionMetadataViewModel();
                    }
                }
            }
            if (metadata == null)
                metadata = new ContributionMetadataViewModel();
            metadata.Url = request.Url;

            return Ok(metadata);
        }

        public class GetMetadataRequest
        {
            public string Url { get; set; }
        }

        public class RouteMatcher
        {
            public static RouteValueDictionary Match(string routeTemplate, string requestPath)
            {
                var template = TemplateParser.Parse(routeTemplate);

                var matcher = new TemplateMatcher(template, GetDefaults(template));

                var values = new RouteValueDictionary();
                var moduleMatch = matcher.TryMatch(requestPath, values);
                return values;
            }

            // This method extracts the default argument values from the template.
            private static RouteValueDictionary GetDefaults(RouteTemplate parsedTemplate)
            {
                var result = new RouteValueDictionary();

                foreach (var parameter in parsedTemplate.Parameters)
                {
                    if (parameter.DefaultValue != null)
                    {
                        result.Add(parameter.Name, parameter.DefaultValue);
                    }
                }

                return result;
            }
        }

        public class Here
        {
            public string First { get; set; }

            public string Second { get; set; }
        }
    }
}
