using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Enums.FCM
{
    public enum CategoryTypeEnum
    {
        Community,
        Chat,
        SessionContent,
        EnrollmentSale,

    }

    public enum CommunityTypeEnum
    {
        PostLike,
        CommentLike,
        Comment,
        Tag,
        FirstPost,
        PinPost
    }
    public enum ChatTypeEnum
    {
        DirectMessage,
        GroupMessage,
    }
    public enum SessionContentTypeEnum
    {
        SessionIsLive,
        OneHourSession,
        TwentyFourHourSession,
        NewLiveSession,
        RescheduledSession,
        CanceledSession,
        SelfPacedAvailable,
        NewSelfPacedAvailable,
    }
    public enum EnrollmentSaleTypeEnum
    {
        CreditCardFail,
        JoinFreeContribution,
        BookFreeSession,
        JoinPaidContribution,
        BookPaidSession,
        ClientBooked
    }
    public enum PermissionTypeEnum
    {
        Off = 0,
        On = 1,
        OnylCoach =2, 
    }

}
