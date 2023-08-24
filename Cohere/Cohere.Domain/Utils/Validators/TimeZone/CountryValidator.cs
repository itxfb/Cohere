using Cohere.Domain.Models.TimeZone;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.TimeZone
{
    public class CountryValidator : AbstractValidator<CountryViewModel>
    {
        public CountryValidator()
        {
            RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("{PropertyName} is required");

            RuleFor(x => x.Alpha2Code)
                .NotEmpty()
                .WithMessage("{PropertyName} is required");

            RuleFor(x => x.StripeSupportedCountry)
                .NotNull()
                .WithMessage("{PropertyName} is required");
		}
    }
}
