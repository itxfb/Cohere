using Microsoft.AspNetCore.Authorization;

namespace Cohere.Api.Auth
{
    public class IsOwnerOrAdminAuthorizationRequirement : IAuthorizationRequirement
    {
    }
}
