using System.Security.Claims;
using Cohere.Api.Utils.Abstractions;
using Cohere.Domain.Models.Account;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Utils
{
    public class CohereController : ControllerBase
    {
        private readonly ITokenGenerator _tokenGenerator;
        public CohereController(ITokenGenerator tokenGenerator = null)
        {
            _tokenGenerator = tokenGenerator;
        }
        public string AccountId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        //public string AccountEmail => User.FindFirst(ClaimTypes.Email)?.Value;

        protected void AddOAuthTokenToResponseHeader(AccountViewModel model)
        {
            HttpContext.Response.Headers.AccessControlExposeHeaders = "o-auth-token";
            HttpContext.Response.Headers.Add("o-auth-token", _tokenGenerator?.GenerateToken(model));
        }
    }
}
