using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary
{
	public class ZoomMeetingData
	{
		public long MeetingId { get; set; }

		public string StartUrl { get; set; }

		public string JoinUrl { get; set; }

		//TODO: Remove after zoom approval
		[Obsolete]
		public string RecordingFileName { get; set; }

		//TODO: Remove after zoom approval
		[Obsolete]
		public string ChatFileName { get; set; }

		public List<string> ChatFiles { get; set; } = new List<string>();
	}
}
