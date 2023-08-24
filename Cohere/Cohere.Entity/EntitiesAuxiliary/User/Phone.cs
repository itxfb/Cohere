using Cohere.Entity.Enums.User;

namespace Cohere.Entity.EntitiesAuxiliary.User
{
    public class Phone
    {
        public string PhoneNumber { get; set; }

        public PhoneTypes PhoneType { get; set; }
    }
}
