using Cohere.Domain.Models.Video;
using FluentValidation;

namespace Cohere.Domain.Utils.Validators.Video
{
    public class DeleteRoomValidator : AbstractValidator<DeleteRoomInfoViewModel>
    {
        public DeleteRoomValidator()
        {
            RuleFor(x => x.ContributionId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Contribution Id should not be empty")
                .MaximumLength(50).WithMessage("Contribution Id maximum length is {MaxLength}");

            RuleFor(x => x.RoomId).Cascade(CascadeMode.StopOnFirstFailure)
                .NotEmpty().WithMessage("Room Id to delete should not be empty")
                .MaximumLength(50).WithMessage("Room Id maximum length is {MaxLength}");
        }
    }
}
