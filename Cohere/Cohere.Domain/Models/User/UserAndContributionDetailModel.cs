using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.User
{
    public class UserAndContributionDetailModel
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        [JsonIgnore]
        public string AccountId { get; set; }
        public string ContributionName { get; set; }
        public string ContributionType { get; set; }
        public string ClientEmail { get; set; }
        [JsonIgnore]
        public DateTime CreateDateTime { get; set; }
    }
    public class ContributionModel
    {
        public string Id { get; set; }
        public string ContributionName { get; set; }
        [JsonIgnore]
        public DateTime CreateDateTime { get; set; }
    }
}
