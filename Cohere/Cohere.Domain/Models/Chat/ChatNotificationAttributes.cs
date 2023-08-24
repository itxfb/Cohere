using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.Chat
{
    public class ChatNotificationAttributes
    {
        public bool IsGroupChat { get; set; }
        public List<string> MemberEmails { get; set; }
        public string ChannelSid { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ContributionId { get; set; } = string.Empty;

    }
}
