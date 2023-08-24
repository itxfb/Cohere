using Cohere.Domain.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Utils.Extensions
{
    public static class OperationResultExtensions
    {
        public static IActionResult ToActionResult(this OperationResult result)
        {
            if (result.Succeeded)
            {
                return new ObjectResult(result.Payload);
            }

            return new BadRequestObjectResult(result.Message);
        }
    }
}
