using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.EntitiesAuxiliary.ZoomWebhooks
{
    public class Object
    {
        public string uuid { get; set; }

        public long id { get; set; }

        public string account_id { get; set; }

        public string host_id { get; set; }

        public string topic { get; set; }

        public int type { get; set; }

        public DateTime start_time { get; set; }

        public string timezone { get; set; }

        public string host_email { get; set; }

        public int duration { get; set; }

        public int total_size { get; set; }

        public int recording_count { get; set; }

        public string share_url { get; set; }

        public List<RecordingFile> recording_files { get; set; }

        public string password { get; set; }
    }
}
