using Cohere.Domain.Models.Video;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Video
{
    public class TwilioWebHookModelValidator : AbstractValidator<TwilioVideoWebHookModel>
    {
        public TwilioWebHookModelValidator()
        {
            RuleFor(x => x.RoomStatus).NotEmpty();
            RuleFor(x => x.RoomType).NotEmpty();
            RuleFor(x => x.RoomSid).NotEmpty();
            RuleFor(x => x.RoomName).NotEmpty();
            RuleFor(x => x.StatusCallbackEvent).NotEmpty();
            RuleFor(x => x.Timestamp).NotEmpty();
            RuleFor(x => x.AccountSid).NotEmpty();
        }
    }
}
