using Cohere.Domain.Models.TimeZone;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.TimeZone
{
    public class TimeZoneValidator : AbstractValidator<TimeZoneViewModel>
    {
        public TimeZoneValidator()
        {
            RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("{PropertyName} is required");

            RuleFor(x => x.CountryName)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");

            RuleFor(x => x.CountryId)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");
		}
    }
}
