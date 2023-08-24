using Twilio.Rest.Video.V1;

namespace Cohere.Domain.Models.Video
{
    public class CreatedRoomAndGetTokenViewModel : GetTokenViewModel
    {
        public RoomResource Room { get; set; }
    }
}
