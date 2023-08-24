using FluentValidation;

namespace Cohere.Domain.Models.ContributionViewModels.Shared
{
    public class ContributionCourseViewModel : SessionBasedContributionViewModel
    {
        public ContributionCourseViewModel(IValidator<ContributionCourseViewModel> validator)
            : base(validator)
        {
        }
    }
}
