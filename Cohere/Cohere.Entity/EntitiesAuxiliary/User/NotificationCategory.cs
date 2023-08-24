using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Entity.EntitiesAuxiliary.User
{
    public class NotificationCategory
    {
        public string Name { get; set; }
        public List<SpecificNotification> SpecificNotifications { get; set; } = new List<SpecificNotification>();
    }
}
