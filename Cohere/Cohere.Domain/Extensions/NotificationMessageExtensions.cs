using Cohere.Entity.Enums.FCM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Extensions
{
    public static class NotificationMessageExtensions
    {
        #region CommunityType
        private static readonly Dictionary<CommunityTypeEnum, string> CommunityEnumNames = new Dictionary<CommunityTypeEnum, string>
        {
            { CommunityTypeEnum.PostLike, "_name_ liked your post in _contribution_" },
            { CommunityTypeEnum.CommentLike, "_name_ liked your comment in _contribution_" },
            { CommunityTypeEnum.Comment, "_name_ commented on your post in _contribution_" },
            { CommunityTypeEnum.Tag, "_name_ tagged you in a post in _contribution_" },
            { CommunityTypeEnum.FirstPost, "_name_ made their first post in _contribution_" },
            { CommunityTypeEnum.PinPost, "_name_ pinned a post in _contribution_" }
            
        };

        private static readonly Dictionary<string, CommunityTypeEnum> CommunityNameEnums = CommunityEnumNames.ToDictionary(x => x.Value, y => y.Key);

        public static string GetCommunityTypeName(this CommunityTypeEnum key, string UserName = "", string ContributionName = "")
        {
            var value = CommunityEnumNames[key];
            if(!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(ContributionName))
            {
                value = value.Replace("_name_", UserName);
                value = value.Replace("_contribution_", ContributionName);

            }
            return value;
        }

        public static CommunityTypeEnum GetCommunityTypeEnum(this string key,string UserName="", string ContributionName="")
        {
            
            return CommunityNameEnums[key];
        }

        #endregion

        #region ChatType
        private static readonly Dictionary<ChatTypeEnum, string> ChatEnumNames = new Dictionary<ChatTypeEnum, string>
        {
            { ChatTypeEnum.DirectMessage, "_name_ sent you a message" },
            { ChatTypeEnum.GroupMessage, "_name_ left a message on _contribution_" }
           
        };

        private static readonly Dictionary<string, ChatTypeEnum> ChatNameEnums = ChatEnumNames.ToDictionary(x => x.Value, y => y.Key);

        public static string GetChatTypeName(this ChatTypeEnum key, string UserName = "", string ContributionName = "")
        {
            var value = ChatEnumNames[key];
            
                value = value.Replace("_name_", UserName);
                value = value.Replace("_contribution_", ContributionName);

            
            return value;
        }


        #endregion

        #region SessionContentType
        private static readonly Dictionary<SessionContentTypeEnum, string> SessionContentEnumNames = new Dictionary<SessionContentTypeEnum, string>
        {
            { SessionContentTypeEnum.SessionIsLive, "_sessionName_ is live" },
            { SessionContentTypeEnum.OneHourSession, "_sessionName_ begins <1 hour" },
            { SessionContentTypeEnum.TwentyFourHourSession, "_sessionName_ begins <24 hours" },
            { SessionContentTypeEnum.NewLiveSession, "_name_ added a new session inside _contribution_" },
            { SessionContentTypeEnum.RescheduledSession, "_sessionName_ has been rescheduled " },
            { SessionContentTypeEnum.CanceledSession, "_sessionName_ has been canceled " },
            { SessionContentTypeEnum.SelfPacedAvailable, "_content_ is now available inside _contribution_" },
            { SessionContentTypeEnum.NewSelfPacedAvailable, "_name_ added new content inside _contribution_" },

        };

        private static readonly Dictionary<string, SessionContentTypeEnum> SessionContentNameEnums = SessionContentEnumNames.ToDictionary(x => x.Value, y => y.Key);

        public static string GetSessionContentTypeName(this SessionContentTypeEnum key, string SessionName = "", string ContributionName = "",string UserName="",string Content="")
        {
            var value = SessionContentEnumNames[key];
            
                value = value.Replace("_name_", UserName);
                value = value.Replace("_sessionName_", SessionName);
                value = value.Replace("_contribution_", ContributionName);
                value = value.Replace("_content_", Content);

            return value;
        }


        #endregion


        #region EnrollmentSaleType
        private static readonly Dictionary<EnrollmentSaleTypeEnum, string> EnrollmentSaleEnumNames = new Dictionary<EnrollmentSaleTypeEnum, string>
        {
            { EnrollmentSaleTypeEnum.CreditCardFail, "Your client's card did not processs " },
            { EnrollmentSaleTypeEnum.JoinFreeContribution, "_name_ joined _contribution_, Congrats!" },
            { EnrollmentSaleTypeEnum.BookFreeSession, "_name_ is booked with you" },
            { EnrollmentSaleTypeEnum.JoinPaidContribution, "_name_ paid _amountCurrency_ for _contribution_, Congrats!" },
            { EnrollmentSaleTypeEnum.BookPaidSession, "_name_ paid you _amountCurrency_ and is booked with you" },
            { EnrollmentSaleTypeEnum.ClientBooked, "You are confirmed for _contribution_" }

        };

        private static readonly Dictionary<string, EnrollmentSaleTypeEnum> EnrollmentSaleNameEnums = EnrollmentSaleEnumNames.ToDictionary(x => x.Value, y => y.Key);

        public static string GetEnrollmentSaleTypeName(this EnrollmentSaleTypeEnum key, string ClientName = "", string ContributionName = "", string amountCurrency="")
        {
            var value = EnrollmentSaleEnumNames[key];
            
                value = value.Replace("_name_", ClientName);
                value = value.Replace("_contribution_", ContributionName);
                value = value.Replace("_amountCurrency_", amountCurrency);

            
            return value;
        }


        #endregion
    }
}
