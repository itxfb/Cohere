using Cohere.Domain.Models.ModelsAuxiliary;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators
{
    public class LocationValidator : AbstractValidator<LocationViewModel>
    {
        public LocationValidator()
        {
            RuleFor(u => u.Latitude).NotNull().WithMessage("{PropertyName} should not be empty");

            RuleFor(u => u.Longitude).NotNull().WithMessage("{PropertyName} should not be empty");
        }
    }
}
