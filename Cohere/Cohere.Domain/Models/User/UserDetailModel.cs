using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.User
{
    public class UserDetailModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AvatarUrl { get; set; }
        public string TimeZoneId { get; set; }
        public string Bio { get; set; }
        public string CountryName { get; set; }
        public string TimeZoneShortForm { get; set; }
    }
}
