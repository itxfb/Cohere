using System;
using Cohere.Domain.Models.ContributionViewModels.Shared;
using Cohere.Domain.Utils.Validators.Contribution;
using Cohere.Entity.Entities.Contrib;
using Newtonsoft.Json.Linq;

namespace Cohere.Domain.Utils
{
    public class ContributionJsonConverter : JsonCreationConverter<ContributionBaseViewModel>
    {
        protected override ContributionBaseViewModel Create(Type objectType, JObject jObject)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException("jObject");
            }

            return jObject["type"]?.Value<string>() switch
            {
                nameof(ContributionCourse) => new ContributionCourseViewModel(new ContributionCourseValidator()),
                nameof(ContributionOneToOne) => new ContributionOneToOneViewModel(new ContributionOneToOneValidator()),
                nameof(ContributionMembership) => new ContributionMembershipViewModel(new ContributionMembershipValidator()),
                nameof(ContributionCommunity) => new ContributionCommunityViewModel(new ContributionCommunityValidator()),
                _ => null
            };
        }
    }
}
