using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Cohere.Api.Controllers;
using Cohere.Domain.Models.Account;
using Cohere.Domain.Models.User;
using Cohere.Entity.Entities;
using Cohere.Entity.Entities.Contrib;
using Cohere.Entity.Enums;
using Cohere.Entity.UnitOfWork;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Cohere.Api.Auth
{
    public class IsOwnerOrAdminAuthorizationHandler : AuthorizationHandler<IsOwnerOrAdminAuthorizationRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUnitOfWork _unitOfWork;

        public IsOwnerOrAdminAuthorizationHandler(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork)
        {
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsOwnerOrAdminAuthorizationRequirement requirement)
        {
            if (context.User == null)
            {
                return;
            }

            var user = context.User;

            if (!user.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            {
                return;
            }

            if (user.IsInRole(Roles.Admin.ToString()) || user.IsInRole(Roles.SuperAdmin.ToString()))
            {
                context.Succeed(requirement);
                return;
            }

            var accountId = context.User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier).Value;

            var request = _httpContextAccessor.HttpContext.Request;

            bool isAuthorized;
            if (request.ContentLength > 0)
            {
                isAuthorized = await ParseBodyAndAuthorize(request, accountId);
            }
            else
            {
                isAuthorized = await ParseRouteParameterAndAuthorize(request, accountId);
            }

            if (isAuthorized)
            {
                context.Succeed(requirement);
            }
        }

        private async Task<bool> ParseBodyAndAuthorize(
            HttpRequest request,
            string accountId)
        {
            request.EnableBuffering();

            string bodyString;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                bodyString = await reader.ReadToEndAsync();
            }

            request.Body.Position = 0;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var userVm = JsonSerializer.Deserialize<UserViewModel>(bodyString, options);

            if (accountId == userVm.AccountId)
            {
                return true;
            }

            var changePasswordViewModel = JsonSerializer.Deserialize<ChangePasswordViewModel>(bodyString, options);
            if (changePasswordViewModel.CurrentPassword != null)
            {
                var account = await _unitOfWork.GetRepositoryAsync<Account>().GetOne(a => a.Email == changePasswordViewModel.Email);
                return account.Id == accountId;
            }

            return false;
        }

        private async Task<bool> ParseRouteParameterAndAuthorize(
            HttpRequest request,
            string accountId)
        {
            var id = request.RouteValues["id"].ToString();
            var controllerName = request.RouteValues["controller"].ToString();

            if (controllerName.Equals(nameof(UserController).Replace("Controller", string.Empty), StringComparison.CurrentCultureIgnoreCase))
            {
                var userFromDb = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.Id == id);
                if (userFromDb.AccountId == accountId)
                {
                    return true;
                }
            }

            if (controllerName.Equals(nameof(ContributionController).Replace("Controller", string.Empty), StringComparison.CurrentCultureIgnoreCase))
            {
                var userFromDb = await _unitOfWork.GetRepositoryAsync<User>().GetOne(u => u.AccountId == accountId);

                if (request.Method == HttpMethods.Get)
                {
                    if (userFromDb.Id == id)
                    {
                        return true;
                    }
                }

                if (request.Method == HttpMethods.Put || request.Method == HttpMethods.Delete)
                {
                    var contributionsFromDb = await _unitOfWork.GetRepositoryAsync<ContributionBase>()
                        .GetOne(e => e.Id == id && e.UserId == userFromDb.Id);

                    return contributionsFromDb != null;
                }
            }

            return false;
        }
    }
}
