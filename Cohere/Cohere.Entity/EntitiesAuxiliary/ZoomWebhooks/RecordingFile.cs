using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
    public class RecordingFile
    {
        public string id { get; set; }
        public string meeting_id { get; set; }
        public DateTime recording_start { get; set; }
        public DateTime recording_end { get; set; }
        public string file_type { get; set; }
        public string file_extension { get; set; }
        public int file_size { get; set; }
        public string play_url { get; set; }
        public string download_url { get; set; }
        public string status { get; set; }
        public string recording_type { get; set; }
    }
}
