using Cohere.Entity.EntitiesAuxiliary.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.User
{
    public class UserDTO
    {
        public string Id { get; set; }
        public ClientPreferences ClientPreferences { get; set; } = null;
        public bool IsStandardTaxEnabled { set; get; } = false;
        public string DefaultPaymentMethod { get; set; } = string.Empty;
        public string CountryId { get; set; } = string.Empty;
    }
}
