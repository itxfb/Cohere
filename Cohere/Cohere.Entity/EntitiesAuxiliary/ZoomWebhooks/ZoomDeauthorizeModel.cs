using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
	public class ZoomDeauthorizeModel
	{
		public string @event { get; set; }

		public DeauthorizePayload payload { get; set; }
	}
}
