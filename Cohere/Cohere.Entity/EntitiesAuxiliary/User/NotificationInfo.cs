using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.EntitiesAuxiliary.User
{
    public class NotificationInfo
    {
        public string Name { get; set; } // SpecificNotification name
        public string Id { get; set; } // it can be commentId/PostId etc
        public string ContributionId { get; set; }

    }
}
