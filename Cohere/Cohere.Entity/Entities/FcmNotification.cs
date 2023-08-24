using Cohere.Entity.EntitiesAuxiliary.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.Entities
{
    public class FcmNotification : BaseEntity
    {
        public string SenderUserId { get; set; }
        public string Image { get; set; }

        public string ReceiverUserId { get; set; }

        public int IsRead { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string UserType { get; set; }     
        public Dictionary<string,string> NotificationInfo { get; set; }


    }
}
