using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Infrastructure.Options
{
	public class LoggingSettings
	{
		public string CloudWatchLogGroup { get; set; }

		public int MinimumLogEventLevel { get; set; }
	}
}
