using Cohere.Domain.Models.Affiliate;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Affiliate
{
    public class InviteEmailRequestModelValidator : AbstractValidator<InviteEmailsRequestModel>
    {
        public InviteEmailRequestModelValidator()
        {
            RuleFor(x => x.EmailAddresses)
                .NotNull()
                .NotEmpty();
        }
    }
}
