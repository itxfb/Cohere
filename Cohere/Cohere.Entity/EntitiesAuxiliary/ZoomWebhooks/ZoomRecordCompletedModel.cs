using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
	public class ZoomRecordCompletedModel
	{
		public Payload payload { get; set; }
		public long event_ts { get; set; }
		public string @event { get; set; }
		public string download_token { get; set; }
	}
}
