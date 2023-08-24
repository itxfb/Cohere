using Cohere.Domain.Models.User;

namespace Cohere.Domain.Models.Account
{
    public class AccountAndUserWithRolesAggregateViewModel : RolesViewModel
    {
        public AccountViewModel Account { get; set; }

        public UserViewModel User { get; set; }
    }
}
