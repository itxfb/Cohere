using Cohere.Domain.Models.Account;

namespace Cohere.Api.Utils.Abstractions
{
    public interface ITokenGenerator
    {
        string GenerateToken(AccountViewModel accountVm);
    }
}
