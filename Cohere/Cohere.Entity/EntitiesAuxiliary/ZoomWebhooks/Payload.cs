using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
    public class Payload
    {
        public string account_id { get; set; }
        public Object @object { get; set; }
        public string plainToken { set; get; }
    }
}
