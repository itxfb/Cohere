using Cohere.Domain.Models.Video;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Video
{
    public class TwilioCompositionWebHookModelValidator : AbstractValidator<TwilioCompositionWebHookModel>
    {
        public TwilioCompositionWebHookModelValidator()
        {
        }
    }
}
