using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
	public class DeauthorizePayload
	{
		public string account_id { get; set; }
		public string user_id { get; set; }
		public string signature { get; set; }
		public DateTime deauthorization_time { get; set; }
		public string client_id { get; set; }
	}
}
